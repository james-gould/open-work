using System.Security.Cryptography;

namespace OpenWork.Api.Services;

/// <summary>
/// PBKDF2-based PIN hashing. A PIN is short (often 4-8 digits), so a slow, salted,
/// high-iteration hash matters here even more than for a typical password - this is the
/// one thing standing between "anyone with the URL" and "only me", per INTENT.md.
/// </summary>
public static class PinHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    public const int DefaultIterations = 210_000;

    public static (byte[] Hash, byte[] Salt, int Iterations) Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, DefaultIterations, HashAlgorithmName.SHA256, HashSize);
        return (hash, salt, DefaultIterations);
    }

    public static bool Verify(string pin, byte[] hash, byte[] salt, int iterations)
    {
        var computed = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, hash.Length);
        return CryptographicOperations.FixedTimeEquals(computed, hash);
    }
}
