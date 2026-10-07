namespace OpenWork.Api.Models;

/// <summary>
/// Singleton row holding the server's VAPID key pair for Web Push. Generated once on
/// first run and persisted so it survives container restarts (and existing client
/// subscriptions stay valid).
/// </summary>
public class VapidKeySet
{
    /// <summary>Always 1 - this table only ever has a single row.</summary>
    public int Id { get; set; } = 1;

    public required string PublicKey { get; set; }

    public required string PrivateKey { get; set; }
}
