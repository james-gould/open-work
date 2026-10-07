namespace OpenWork.Api.Models;

/// <summary>
/// The urgency bucket a work item is displayed under. The numeric value doubles as the
/// sort key, so Critical (0) always renders above Warning (1), which always renders
/// above Acceptable (2).
/// </summary>
public enum WorkItemCategory
{
    Critical = 0,
    Warning = 1,
    Acceptable = 2
}
