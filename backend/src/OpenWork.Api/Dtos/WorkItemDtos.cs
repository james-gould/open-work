using OpenWork.Api.Models;

namespace OpenWork.Api.Dtos;

/// <summary>What the client receives for each work item, including the resolved display category.</summary>
public record WorkItemResponse(
    Guid Id,
    string Title,
    string? Body,
    int Priority,
    DateTimeOffset? RequiresActionAt,
    DateTimeOffset? CompletionDueAt,
    WorkItemCategory Category,
    bool IsManualCategory,
    bool IsDone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record CreateWorkItemRequest(
    string Title,
    string? Body,
    int Priority,
    DateTimeOffset? RequiresActionAt,
    DateTimeOffset? CompletionDueAt
);

/// <summary>
/// Partial update. Every property is an explicit "was this field sent" flag + value pair
/// so a client can distinguish "leave unchanged" from "clear this field" (e.g. clearing a
/// manual category override back to automatic).
/// </summary>
public record UpdateWorkItemRequest(
    string? Title = null,
    bool BodySet = false,
    string? Body = null,
    int? Priority = null,
    bool RequiresActionAtSet = false,
    DateTimeOffset? RequiresActionAt = null,
    bool CompletionDueAtSet = false,
    DateTimeOffset? CompletionDueAt = null,
    bool ManualCategorySet = false,
    WorkItemCategory? ManualCategory = null,
    bool? IsDone = null
);

public record PushSubscriptionKeys(string P256dh, string Auth);

public record PushSubscriptionRequest(string Endpoint, PushSubscriptionKeys Keys);

public record PushUnsubscribeRequest(string Endpoint);
