namespace OpenWork.Api.Models;

/// <summary>A browser Push API subscription registered by a client device.</summary>
public class PushSubscriptionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Endpoint { get; set; }

    public required string P256dh { get; set; }

    public required string Auth { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
