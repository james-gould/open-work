using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OpenWork.Api.Services;

/// <summary>
/// Mints the single long-lived bearer token issued on PIN setup/login. 365-day expiry is
/// deliberate: this app is meant to be driven by an unattended agent (the Daily Driver
/// sweep) as much as a browser, so a short-lived token that silently expires would
/// reintroduce the "it stops working and nobody notices" failure mode INTENT.md is
/// trying to avoid elsewhere. Re-authenticating (with the PIN) whenever you like still
/// invalidates nothing else, since there's only one subject.
/// </summary>
public class JwtTokenService(IConfiguration configuration)
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(365);

    private SymmetricSecurityKey SigningKey
    {
        get
        {
            var key = configuration["Auth:SigningKey"]
                ?? throw new InvalidOperationException("Auth:SigningKey is required.");
            if (key.Length < 32)
            {
                throw new InvalidOperationException("Auth:SigningKey must be at least 32 characters.");
            }

            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        }
    }

    public (string Token, DateTimeOffset ExpiresAt) IssueToken()
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.Add(TokenLifetime);
        var credentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, "owner")],
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public TokenValidationParameters ValidationParameters => new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
}
