namespace OpenWork.Api.Models;

/// <summary>
/// Where a work item sits in its own progression, independent of its urgency
/// <see cref="WorkItemCategory"/>. This is what lets a caller (e.g. the Daily Driver
/// reminder sweep) tell "pending, needs starting" apart from "already being worked" -
/// a Critical item that is already InProgress should not generate a reminder.
/// </summary>
public enum WorkItemStatus
{
    Backlog = 0,
    InProgress = 1,
    Blocked = 2,
    Done = 3
}
