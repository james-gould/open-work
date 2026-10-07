namespace OpenWork.Api.Models;

/// <summary>
/// A single piece of outstanding work being tracked (a PR awaiting your review response,
/// a task you queued up, a reminder, etc.).
/// </summary>
public class WorkItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Title { get; set; }

    public string? Body { get; set; }

    /// <summary>User-assigned priority number. Higher = more important. Used as the final tie-breaker.</summary>
    public int Priority { get; set; }

    /// <summary>
    /// When the item entered a "the ball is in your court" state (e.g. a reviewer replied,
    /// a blocker cleared). Null means nothing is currently waiting on you for this item.
    /// Driving sort/category off this timestamp is what surfaces state changes that put
    /// work back on you, instead of letting them silently sit unseen.
    /// </summary>
    public DateTimeOffset? RequiresActionAt { get; set; }

    /// <summary>Target/deadline date for the item, if any. Drives the "looming deadline" escalation.</summary>
    public DateTimeOffset? CompletionDueAt { get; set; }

    /// <summary>
    /// Manual override of the computed category. When set, this always wins over the
    /// derived category so a stale "Critical" can be downgraded (or vice versa).
    /// </summary>
    public WorkItemCategory? ManualCategory { get; set; }

    public bool IsDone { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Last time a push nudge was sent for this item while Critical, to debounce repeats.</summary>
    public DateTimeOffset? LastNudgedAt { get; set; }
}
