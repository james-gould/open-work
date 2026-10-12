using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenWork.Api.Data;
using OpenWork.Api.Models;
using WebPush;

namespace OpenWork.Api.Services;

/// <summary>
/// Background loop that is the "remote nudge" mechanism: it periodically re-evaluates
/// every open item that isn't already InProgress and, for anything that is (still) Critical, pushes a browser
/// notification to every registered device so a stalled item is surfaced even if the
/// app isn't open in a tab. Repeats are debounced via <see cref="Prioritization.NudgeCooldown"/>.
/// </summary>
public class NudgeBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<NudgeBackgroundService> logger,
    IConfiguration configuration) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the host a moment to finish starting before the first sweep.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Nudge sweep failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var vapidProvider = scope.ServiceProvider.GetRequiredService<VapidKeyProvider>();

        var subscriptions = await db.PushSubscriptions.ToListAsync(ct);
        if (subscriptions.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        // InProgress items are already being worked, and Waiting items can't be acted on until
        // something else happens - neither needs a nudge. Items not yet startable
        // (future StartAt) are filtered out below, once urgency has been resolved.
        var items = await db.WorkItems
            .Where(w => w.Status != WorkItemStatus.Done && w.Status != WorkItemStatus.InProgress
                && w.Status != WorkItemStatus.Waiting)
            .ToListAsync(ct);

        var dueForNudge = items
            .Where(w => !Prioritization.IsPendingStart(w, now))
            .Where(w => Prioritization.Resolve(w, now) == WorkItemCategory.Critical)
            .Where(w => w.LastNudgedAt is null || now - w.LastNudgedAt >= Prioritization.NudgeCooldown)
            .ToList();

        if (dueForNudge.Count == 0)
        {
            return;
        }

        var vapid = await vapidProvider.GetOrCreateAsync(ct);
        var subject = configuration["Vapid:Subject"] ?? "mailto:open-work@example.com";
        var vapidDetails = new VapidDetails(subject, vapid.PublicKey, vapid.PrivateKey);
        var client = new WebPushClient();

        foreach (var item in dueForNudge)
        {
            var payload = JsonSerializer.Serialize(new
            {
                title = $"Needs your attention: {item.Title}",
                body = string.IsNullOrWhiteSpace(item.Body) ? "This item is Critical and waiting on you." : item.Body,
                itemId = item.Id
            });

            foreach (var sub in subscriptions.ToList())
            {
                var pushSubscription = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                try
                {
                    await client.SendNotificationAsync(pushSubscription, payload, vapidDetails, ct);
                }
                catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
                {
                    // Subscription is no longer valid on the browser's end - stop tracking it.
                    db.PushSubscriptions.Remove(sub);
                    subscriptions.Remove(sub);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to send push nudge for item {ItemId}", item.Id);
                }
            }

            item.LastNudgedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }
}
