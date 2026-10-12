using OpenWork.Api.Dtos;
using OpenWork.Api.Models;

namespace OpenWork.Api.Services;

/// <summary>
/// Pure, documented rules for turning a WorkItem's raw dates into a display category and
/// sort order. Kept in one place so the thresholds below are easy to find and tune.
/// </summary>
public static class Prioritization
{
    /// <summary>How soon before a "requires action" date counts as Warning instead of Acceptable.</summary>
    public static readonly TimeSpan ActionWarningLookahead = TimeSpan.FromHours(2);

    /// <summary>How soon before a completion deadline counts as Warning instead of Acceptable.</summary>
    public static readonly TimeSpan CompletionWarningWindow = TimeSpan.FromDays(2);

    /// <summary>Minimum gap between repeat push nudges for the same Critical item.</summary>
    public static readonly TimeSpan NudgeCooldown = TimeSpan.FromMinutes(30);

    /// <summary>
    /// True when the item has a future <see cref="WorkItem.StartAt"/> - i.e. it genuinely
    /// cannot be started yet, regardless of how urgent its due date makes it look. Items in
    /// this state are kept out of the main list, the "pick up next" query, and nudges, but
    /// urgency (<see cref="Resolve"/>) is computed independently so the due-date escalation
    /// still applies the moment it becomes startable.
    /// </summary>
    public static bool IsPendingStart(WorkItem item, DateTimeOffset now) =>
        item.StartAt is { } startAt && startAt > now;

    public static WorkItemCategory Resolve(WorkItem item, DateTimeOffset now)
    {
        if (item.ManualCategory is { } manual)
        {
            return manual;
        }

        var actionOverdue = item.RequiresActionAt is { } ra && ra <= now;
        var completionOverdue = item.CompletionDueAt is { } cd && cd <= now;
        if (actionOverdue || completionOverdue)
        {
            return WorkItemCategory.Critical;
        }

        var actionSoon = item.RequiresActionAt is { } ra2 && ra2 - now <= ActionWarningLookahead;
        var completionSoon = item.CompletionDueAt is { } cd2 && cd2 - now <= CompletionWarningWindow;
        if (actionSoon || completionSoon)
        {
            return WorkItemCategory.Warning;
        }

        return WorkItemCategory.Acceptable;
    }

    /// <summary>
    /// Orders items for display: category group first (Critical, then Warning, then
    /// Acceptable), then by "requires action" date descending (most recent "it's your turn"
    /// state change first, per INTENT.md; items with no action date sink to the bottom of
    /// their group), then by looming completion date ascending, then by the user's manual
    /// priority number descending (higher = more important).
    /// </summary>
    public static IEnumerable<WorkItem> Sort(IEnumerable<WorkItem> items, DateTimeOffset now)
    {
        return items
            .Select(item => (Item: item, Category: Resolve(item, now)))
            .OrderBy(x => x.Category)
            .ThenByDescending(x => x.Item.RequiresActionAt ?? DateTimeOffset.MinValue)
            .ThenBy(x => x.Item.CompletionDueAt ?? DateTimeOffset.MaxValue)
            .ThenByDescending(x => x.Item.Priority)
            .Select(x => x.Item);
    }

    public static WorkItemResponse ToResponse(WorkItem item, DateTimeOffset now) => new(
        item.Id,
        item.Title,
        item.Body,
        item.Reference,
        item.ExternalKey,
        item.Priority,
        item.RequiresActionAt,
        item.CompletionDueAt,
        item.StartAt,
        Resolve(item, now),
        item.ManualCategory is not null,
        item.Status,
        item.IsDone,
        item.CreatedAt,
        item.UpdatedAt,
        item.CompletedAt
    );
}
