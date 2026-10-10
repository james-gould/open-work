namespace OpenWork.Api.Models;

/// <summary>
/// Singleton row holding the app's single PIN (hashed, never stored in plain text) and,
/// optionally, one WebAuthn/passkey credential used only for the "I forgot my PIN"
/// recovery flow. A blank install has no row at all - its absence IS the "not yet set
/// up" state, which is what lets the frontend show "create a PIN" instead of "enter your
/// PIN" on first run.
/// </summary>
public class AuthConfig
{
    /// <summary>Always 1 - this table only ever has a single row.</summary>
    public int Id { get; set; } = 1;

    /// <summary>PBKDF2 hash of the PIN. Never the PIN itself.</summary>
    public required byte[] PinHash { get; set; }

    public required byte[] PinSalt { get; set; }

    public required int PinIterations { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Consecutive failed login attempts, for the lockout below. Reset on success.</summary>
    public int FailedAttempts { get; set; }

    /// <summary>While in the future, PIN login is refused regardless of correctness.</summary>
    public DateTimeOffset? LockedUntil { get; set; }

    // --- Recovery passkey (WebAuthn), optional ---

    public byte[]? RecoveryCredentialId { get; set; }

    public byte[]? RecoveryPublicKey { get; set; }

    public uint RecoverySignCount { get; set; }

    public byte[]? RecoveryUserHandle { get; set; }

    public bool RecoveryBackupEligible { get; set; }

    public DateTimeOffset? RecoveryRegisteredAt { get; set; }

    // --- In-flight WebAuthn ceremony state ---
    // Fido2NetLib's verification step needs the exact options object it handed out
    // (challenge, RP, user, etc.), not just the raw challenge bytes, so the full
    // serialized options are stashed here between the "options" and "verify" calls.
    // A single pending-ceremony slot is enough for a single-user app: there is never
    // more than one registration/recovery ceremony happening at once.

    public string? PendingCeremonyOptionsJson { get; set; }

    public DateTimeOffset? PendingCeremonyExpiresAt { get; set; }
}
