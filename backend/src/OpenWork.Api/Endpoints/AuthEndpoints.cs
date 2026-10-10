using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using OpenWork.Api.Data;
using OpenWork.Api.Dtos;
using OpenWork.Api.Models;
using OpenWork.Api.Services;

namespace OpenWork.Api.Endpoints;

/// <summary>
/// PIN-based auth (login/setup) plus the WebAuthn "I forgot my PIN" recovery ceremony.
/// A single-user app, so there is exactly one <see cref="AuthConfig"/> row and one
/// (optional) recovery passkey - no username/email is ever collected.
/// </summary>
public static class AuthEndpoints
{
    // How long a WebAuthn ceremony's options remain valid before the client must ask again.
    private static readonly TimeSpan CeremonyLifetime = TimeSpan.FromMinutes(5);

    // After this many wrong PINs in a row, login is refused for a while even if the next
    // guess would have been correct - slows down brute-forcing a short PIN.
    private const int MaxFailedAttempts = 6;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // There is only ever one "user" in this app; WebAuthn still wants a stable user handle.
    private static readonly byte[] RecoveryUserId = "owner"u8.ToArray();

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/status", GetStatus);
        group.MapPost("/setup", Setup);
        group.MapPost("/login", Login);

        // Registering a recovery passkey happens while already logged in (you prove you
        // still have the PIN before you're allowed to add a "forgot the PIN" bypass for it).
        group.MapPost("/recovery/register-options", RecoveryRegisterOptions).RequireAuthorization();
        group.MapPost("/recovery/register", RecoveryRegister).RequireAuthorization();

        // Using the recovery passkey is, by definition, for when you're locked out - so
        // these two stay anonymous.
        group.MapPost("/recovery/assertion-options", RecoveryAssertionOptions);
        group.MapPost("/recovery/wipe", RecoveryWipe);

        return group;
    }

    private static async Task<Ok<AuthStatusResponse>> GetStatus(AppDbContext db)
    {
        var config = await db.AuthConfig.FindAsync(1);
        return TypedResults.Ok(new AuthStatusResponse(
            Configured: config is not null,
            HasRecovery: config?.RecoveryCredentialId is not null));
    }

    private static async Task<Results<Ok<AuthTokenResponse>, Conflict<string>, ValidationProblem>> Setup(
        AppDbContext db, JwtTokenService jwt, PinRequest request)
    {
        if (ValidatePin(request.Pin) is { } invalid)
        {
            return invalid;
        }

        if (await db.AuthConfig.AnyAsync())
        {
            return TypedResults.Conflict(
                "A PIN is already configured. Log in instead, or use passkey recovery if you've lost it.");
        }

        var (hash, salt, iterations) = PinHasher.Hash(request.Pin);
        db.AuthConfig.Add(new AuthConfig
        {
            Id = 1,
            PinHash = hash,
            PinSalt = salt,
            PinIterations = iterations
        });
        await db.SaveChangesAsync();

        var (token, expiresAt) = jwt.IssueToken();
        return TypedResults.Ok(new AuthTokenResponse(token, expiresAt));
    }

    private static async Task<Results<Ok<AuthTokenResponse>, UnauthorizedHttpResult, NotFound<string>, ValidationProblem>> Login(
        AppDbContext db, JwtTokenService jwt, PinRequest request)
    {
        if (ValidatePin(request.Pin) is { } invalid)
        {
            return invalid;
        }

        var config = await db.AuthConfig.FindAsync(1);
        if (config is null)
        {
            return TypedResults.NotFound("No PIN is configured yet - call setup first.");
        }

        var now = DateTimeOffset.UtcNow;
        if (config.LockedUntil is { } lockedUntil && lockedUntil > now)
        {
            return TypedResults.Unauthorized();
        }

        if (!PinHasher.Verify(request.Pin, config.PinHash, config.PinSalt, config.PinIterations))
        {
            config.FailedAttempts++;
            if (config.FailedAttempts >= MaxFailedAttempts)
            {
                config.LockedUntil = now.Add(LockoutDuration);
                config.FailedAttempts = 0;
            }

            await db.SaveChangesAsync();
            return TypedResults.Unauthorized();
        }

        config.FailedAttempts = 0;
        config.LockedUntil = null;
        await db.SaveChangesAsync();

        var (token, expiresAt) = jwt.IssueToken();
        return TypedResults.Ok(new AuthTokenResponse(token, expiresAt));
    }

    private static ValidationProblem? ValidatePin(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin) || pin.Length < 4 || pin.Length > 12)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pin"] = ["PIN must be between 4 and 12 characters."]
            });
        }

        return null;
    }

    private static async Task<Results<Ok<CredentialCreateOptions>, NotFound>> RecoveryRegisterOptions(
        AppDbContext db, IFido2 fido2)
    {
        var config = await db.AuthConfig.FindAsync(1);
        if (config is null)
        {
            return TypedResults.NotFound();
        }

        var excludeCredentials = config.RecoveryCredentialId is { } existingId
            ? new List<PublicKeyCredentialDescriptor> { new(existingId) }
            : [];

        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User { Id = RecoveryUserId, Name = "owner", DisplayName = "Open Work recovery passkey" },
            ExcludeCredentials = excludeCredentials,
            AuthenticatorSelection = new AuthenticatorSelection
            {
                UserVerification = UserVerificationRequirement.Required,
                ResidentKey = ResidentKeyRequirement.Preferred
            },
            AttestationPreference = AttestationConveyancePreference.None
        });

        config.PendingCeremonyOptionsJson = options.ToJson();
        config.PendingCeremonyExpiresAt = DateTimeOffset.UtcNow.Add(CeremonyLifetime);
        await db.SaveChangesAsync();

        return TypedResults.Ok(options);
    }

    private static async Task<Results<Ok, NotFound, BadRequest<string>>> RecoveryRegister(
        AppDbContext db, IFido2 fido2, AuthenticatorAttestationRawResponse attestationResponse, CancellationToken ct)
    {
        var config = await db.AuthConfig.FindAsync(1);
        if (config is null)
        {
            return TypedResults.NotFound();
        }

        if (!TryTakePendingCeremony(config, out var optionsJson))
        {
            return TypedResults.BadRequest("No passkey registration is in progress - request options again.");
        }

        try
        {
            var options = CredentialCreateOptions.FromJson(optionsJson);
            var credential = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = attestationResponse,
                OriginalOptions = options,
                // Single-user app: any credential is "unique enough" - there's no other account it could clash with.
                IsCredentialIdUniqueToUserCallback = (_, _) => Task.FromResult(true)
            }, cancellationToken: ct);

            config.RecoveryCredentialId = credential.Id;
            config.RecoveryPublicKey = credential.PublicKey;
            config.RecoverySignCount = credential.SignCount;
            config.RecoveryUserHandle = credential.User.Id;
            config.RecoveryBackupEligible = credential.IsBackupEligible;
            config.RecoveryRegisteredAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return TypedResults.Ok();
        }
        catch (Fido2VerificationException ex)
        {
            await db.SaveChangesAsync(ct);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok<AssertionOptions>, NotFound<string>>> RecoveryAssertionOptions(
        AppDbContext db, IFido2 fido2)
    {
        var config = await db.AuthConfig.FindAsync(1);
        if (config?.RecoveryCredentialId is not { } credentialId)
        {
            return TypedResults.NotFound("No recovery passkey has been registered for this installation.");
        }

        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [new PublicKeyCredentialDescriptor(credentialId)],
            UserVerification = UserVerificationRequirement.Required
        });

        config.PendingCeremonyOptionsJson = options.ToJson();
        config.PendingCeremonyExpiresAt = DateTimeOffset.UtcNow.Add(CeremonyLifetime);
        await db.SaveChangesAsync();

        return TypedResults.Ok(options);
    }

    /// <summary>
    /// The actual "I lost my PIN" escape hatch: a verified passkey assertion wipes every
    /// work item, push subscription, and the PIN/recovery credential itself, returning the
    /// installation to a blank-install state. There is deliberately no gentler "just reset
    /// the PIN and keep my data" path - losing the PIN with no passkey means the data is
    /// gone, which is the whole point of requiring strong 2FA before this runs at all.
    /// </summary>
    private static async Task<Results<Ok, NotFound<string>, BadRequest<string>>> RecoveryWipe(
        AppDbContext db, IFido2 fido2, AuthenticatorAssertionRawResponse assertionResponse, CancellationToken ct)
    {
        var config = await db.AuthConfig.FindAsync(1);
        if (config?.RecoveryCredentialId is not { } credentialId || config.RecoveryPublicKey is not { } publicKey)
        {
            return TypedResults.NotFound("No recovery passkey has been registered for this installation.");
        }

        if (!TryTakePendingCeremony(config, out var optionsJson))
        {
            return TypedResults.BadRequest("No passkey sign-in is in progress - request assertion options again.");
        }

        try
        {
            var options = AssertionOptions.FromJson(optionsJson);
            var storedUserHandle = config.RecoveryUserHandle ?? [];
            await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = assertionResponse,
                OriginalOptions = options,
                StoredPublicKey = publicKey,
                StoredSignatureCounter = config.RecoverySignCount,
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) => Task.FromResult(
                    args.UserHandle.SequenceEqual(storedUserHandle) && args.CredentialId.SequenceEqual(credentialId))
            }, cancellationToken: ct);
        }
        catch (Fido2VerificationException ex)
        {
            await db.SaveChangesAsync(ct);
            return TypedResults.BadRequest(ex.Message);
        }

        db.WorkItems.RemoveRange(db.WorkItems);
        db.PushSubscriptions.RemoveRange(db.PushSubscriptions);
        db.AuthConfig.Remove(config);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok();
    }

    /// <summary>Atomically consumes the pending ceremony options, if any are still valid.</summary>
    private static bool TryTakePendingCeremony(AuthConfig config, out string optionsJson)
    {
        if (config.PendingCeremonyOptionsJson is null
            || config.PendingCeremonyExpiresAt is not { } expiresAt
            || expiresAt < DateTimeOffset.UtcNow)
        {
            optionsJson = "";
            config.PendingCeremonyOptionsJson = null;
            config.PendingCeremonyExpiresAt = null;
            return false;
        }

        optionsJson = config.PendingCeremonyOptionsJson;
        config.PendingCeremonyOptionsJson = null;
        config.PendingCeremonyExpiresAt = null;
        return true;
    }
}
