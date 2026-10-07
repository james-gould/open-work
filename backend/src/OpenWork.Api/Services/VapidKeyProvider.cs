using Microsoft.EntityFrameworkCore;
using OpenWork.Api.Data;
using OpenWork.Api.Models;
using WebPush;

namespace OpenWork.Api.Services;

/// <summary>
/// Ensures a stable VAPID key pair exists for this deployment, generating one on first
/// run and persisting it so restarts don't invalidate every client's push subscription.
/// </summary>
public class VapidKeyProvider(AppDbContext db)
{
    public async Task<VapidKeySet> GetOrCreateAsync(CancellationToken ct = default)
    {
        var existing = await db.VapidKeys.FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return existing;
        }

        var keys = VapidHelper.GenerateVapidKeys();
        var created = new VapidKeySet
        {
            Id = 1,
            PublicKey = keys.PublicKey,
            PrivateKey = keys.PrivateKey
        };

        db.VapidKeys.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }
}
