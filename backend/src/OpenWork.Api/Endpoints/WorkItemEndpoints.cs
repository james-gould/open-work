using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using OpenWork.Api.Data;
using OpenWork.Api.Dtos;
using OpenWork.Api.Models;
using OpenWork.Api.Services;

namespace OpenWork.Api.Endpoints;

public static class WorkItemEndpoints
{
    public static RouteGroupBuilder MapWorkItemEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAll);
        group.MapGet("/next", GetNext);
        group.MapPost("/", Create);
        group.MapPatch("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/complete", Complete);
        group.MapPost("/{id:guid}/reopen", Reopen);
        return group;
    }

    /// <summary>
    /// Lists work items, optionally filtered by progression <paramref name="status"/> and/or
    /// urgency <paramref name="category"/> (repeat the query key for multiple values, e.g.
    /// <c>?status=Backlog&amp;status=Blocked</c>). Always sorted top-down by priority
    /// (category, then requires-action age, then due date, then manual priority number).
    /// </summary>
    private static async Task<Ok<List<WorkItemResponse>>> GetAll(
        AppDbContext db, bool includeDone = false, WorkItemStatus[]? status = null, WorkItemCategory[]? category = null)
    {
        var now = DateTimeOffset.UtcNow;
        var query = db.WorkItems.AsQueryable();

        if (status is { Length: > 0 })
        {
            query = query.Where(w => status.Contains(w.Status));
        }
        else if (!includeDone)
        {
            query = query.Where(w => w.Status != WorkItemStatus.Done);
        }

        var items = await query.ToListAsync();
        return TypedResults.Ok(SortAndFilterByCategory(items, category, now));
    }

    /// <summary>
    /// Returns the single highest-priority item that is not Done (and, by default, not
    /// already InProgress) - this is the backing query for the <c>/pending</c> command and
    /// the hourly reminder sweep: "what's the one thing I should pick up next?".
    /// Pass <paramref name="excludeInProgress"/>=false to also see in-flight work.
    /// </summary>
    private static async Task<Results<Ok<WorkItemResponse>, NoContent>> GetNext(
        AppDbContext db, bool excludeInProgress = true, WorkItemCategory[]? category = null)
    {
        var now = DateTimeOffset.UtcNow;
        var query = db.WorkItems.Where(w => w.Status != WorkItemStatus.Done);
        if (excludeInProgress)
        {
            query = query.Where(w => w.Status != WorkItemStatus.InProgress);
        }

        var items = await query.ToListAsync();
        var next = SortAndFilterByCategory(items, category, now).FirstOrDefault();
        return next is null ? TypedResults.NoContent() : TypedResults.Ok(next);
    }

    private static List<WorkItemResponse> SortAndFilterByCategory(
        IEnumerable<WorkItem> items, WorkItemCategory[]? category, DateTimeOffset now)
    {
        var sorted = Prioritization.Sort(items, now);
        if (category is { Length: > 0 })
        {
            var allowed = category.ToHashSet();
            sorted = sorted.Where(w => allowed.Contains(Prioritization.Resolve(w, now)));
        }

        return sorted.Select(w => Prioritization.ToResponse(w, now)).ToList();
    }

    private static async Task<Results<Created<WorkItemResponse>, Ok<WorkItemResponse>, ValidationProblem>> Create(
        AppDbContext db, CreateWorkItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return TitleRequired();
        }

        var now = DateTimeOffset.UtcNow;
        var externalKey = string.IsNullOrWhiteSpace(request.ExternalKey) ? null : request.ExternalKey.Trim();
        if (externalKey is not null)
        {
            var existing = await db.WorkItems.FirstOrDefaultAsync(w => w.ExternalKey == externalKey);
            if (existing is not null)
            {
                return TypedResults.Ok(Prioritization.ToResponse(existing, now));
            }
        }

        var item = new WorkItem
        {
            Title = request.Title.Trim(),
            Body = request.Body,
            Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
            ExternalKey = externalKey,
            Priority = request.Priority,
            RequiresActionAt = request.RequiresActionAt?.ToUniversalTime(),
            CompletionDueAt = request.CompletionDueAt?.ToUniversalTime(),
            ManualCategory = request.ManualCategory,
            Status = request.Status,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.WorkItems.Add(item);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/workitems/{item.Id}", Prioritization.ToResponse(item, now));
    }

    private static ValidationProblem TitleRequired() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["title"] = ["Title is required."]
        });

    private static async Task<Results<Ok<WorkItemResponse>, NotFound, ValidationProblem>> Update(
        AppDbContext db, Guid id, UpdateWorkItemRequest request)
    {
        if (request.Title is not null && string.IsNullOrWhiteSpace(request.Title))
        {
            return TitleRequired();
        }

        var item = await db.WorkItems.FindAsync(id);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        if (request.Title is not null)
        {
            item.Title = request.Title.Trim();
        }

        if (request.BodySet)
        {
            item.Body = request.Body;
        }

        if (request.ReferenceSet)
        {
            item.Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
        }

        if (request.Priority is { } priority)
        {
            item.Priority = priority;
        }

        if (request.RequiresActionAtSet)
        {
            item.RequiresActionAt = request.RequiresActionAt?.ToUniversalTime();
        }

        if (request.CompletionDueAtSet)
        {
            item.CompletionDueAt = request.CompletionDueAt?.ToUniversalTime();
        }

        if (request.ManualCategorySet)
        {
            item.ManualCategory = request.ManualCategory;
        }

        if (request.Status is { } status)
        {
            item.Status = status;
        }
        else if (request.IsDone is { } isDone)
        {
            // Legacy field: still accepted so old clients keep working, mapped onto Status.
            item.Status = isDone ? WorkItemStatus.Done : WorkItemStatus.Backlog;
        }

        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return TypedResults.Ok(Prioritization.ToResponse(item, DateTimeOffset.UtcNow));
    }

    private static async Task<Results<NoContent, NotFound>> Delete(AppDbContext db, Guid id)
    {
        var item = await db.WorkItems.FindAsync(id);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        db.WorkItems.Remove(item);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<WorkItemResponse>, NotFound>> Complete(AppDbContext db, Guid id)
        => await SetDone(db, id, true);

    private static async Task<Results<Ok<WorkItemResponse>, NotFound>> Reopen(AppDbContext db, Guid id)
        => await SetDone(db, id, false);

    private static async Task<Results<Ok<WorkItemResponse>, NotFound>> SetDone(AppDbContext db, Guid id, bool isDone)
    {
        var item = await db.WorkItems.FindAsync(id);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        item.Status = isDone ? WorkItemStatus.Done : WorkItemStatus.Backlog;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return TypedResults.Ok(Prioritization.ToResponse(item, DateTimeOffset.UtcNow));
    }
}
