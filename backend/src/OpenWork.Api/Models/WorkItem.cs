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

    /// <summary>
    /// Optional URL pointing at the thing this item is about (a PR, an issue, a doc, a
    /// chat thread, etc.) - rendered as a clickable link in the UI. Purely informational;
    /// no validation beyond trimming, since it may be any kind of URI (https, mailto, etc.).
    /// </summary>
    public string? Reference { get; set; }

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

    /// <summary>
    /// Where this item sits in its own progression (backlog/in progress/blocked/done),
    /// independent of the urgency <see cref="WorkItemCategory"/>. This is what lets a
    /// query distinguish "Critical but already being worked" from "Critical and
    /// genuinely needs starting" - e.g. the hourly reminder sweep skips InProgress items.
    /// </summary>
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Backlog;

    /// <summary>Back-compat convenience view of <see cref="Status"/> for existing API consumers.</summary>
    public bool IsDone => Status == WorkItemStatus.Done;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Last time a push nudge was sent for this item while Critical, to debounce repeats.</summary>
    public DateTimeOffset? LastNudgedAt { get; set; }
}
