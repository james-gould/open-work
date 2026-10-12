using System.Globalization;
using System.Text;
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
        group.MapGet("/scheduled", GetScheduled);
        group.MapGet("/report/done", DownloadDoneReport);
        group.MapPost("/", Create);
        group.MapPost("/quick", QuickCapture);
        group.MapPatch("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/complete", Complete);
        group.MapPost("/{id:guid}/reopen", Reopen);
        group.MapPost("/{id:guid}/actionable", MarkActionable);
        return group;
    }

    /// <summary>
    /// Lists work items, optionally filtered by progression <paramref name="status"/> and/or
    /// urgency <paramref name="category"/> (repeat the query key for multiple values, e.g.
    /// <c>?status=Backlog&amp;status=Blocked</c>). Always sorted top-down by priority
    /// (category, then requires-action age, then due date, then manual priority number).
    /// Items that can't be started yet (<see cref="Prioritization.IsPendingStart"/>) are left
    /// out by default - see <see cref="GetScheduled"/> for those - since there's nothing to do
    /// about them until their start date arrives.
    /// </summary>
    private static async Task<Ok<List<WorkItemResponse>>> GetAll(
        AppDbContext db, bool includeDone = false, bool includeScheduled = false,
        WorkItemStatus[]? status = null, WorkItemCategory[]? category = null)
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
        if (!includeScheduled)
        {
            items = items.Where(w => !Prioritization.IsPendingStart(w, now)).ToList();
        }

        return TypedResults.Ok(SortAndFilterByCategory(items, category, now));
    }

    /// <summary>
    /// Returns the single highest-priority item that is not Done (and, by default, not
    /// already InProgress) - this is the backing query for the <c>/pending</c> command and
    /// the hourly reminder sweep: "what's the one thing I should pick up next?".
    /// Pass <paramref name="excludeInProgress"/>=false to also see in-flight work. Items that
    /// can't be started yet are never returned here, regardless of how urgent they look.
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
        items = items.Where(w => !Prioritization.IsPendingStart(w, now)).ToList();
        var next = SortAndFilterByCategory(items, category, now).FirstOrDefault();
        return next is null ? TypedResults.NoContent() : TypedResults.Ok(next);
    }

    /// <summary>
    /// Lists not-yet-startable items (future <see cref="WorkItem.StartAt"/>), soonest first,
    /// so they stay reachable for review/editing without cluttering the main list or being
    /// picked up as "next" before there's anything to actually do.
    /// </summary>
    private static async Task<Ok<List<WorkItemResponse>>> GetScheduled(AppDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var items = await db.WorkItems
            .Where(w => w.Status != WorkItemStatus.Done && w.StartAt != null && w.StartAt > now)
            .OrderBy(w => w.StartAt)
            .ToListAsync();

        return TypedResults.Ok(items.Select(w => Prioritization.ToResponse(w, now)).ToList());
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
            StartAt = request.StartAt?.ToUniversalTime(),
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

    /// <summary>
    /// The "park this distraction" capture path: just a title, dropped straight into the
    /// Backlog, with everything else left to fill in later. Deliberately a separate,
    /// narrower endpoint from <see cref="Create"/> rather than the same one with optional
    /// fields, so the frontend's quick-capture UI only ever has to send one field.
    /// </summary>
    private static async Task<Results<Created<WorkItemResponse>, ValidationProblem>> QuickCapture(
        AppDbContext db, QuickCaptureRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return TitleRequired();
        }

        var now = DateTimeOffset.UtcNow;
        var item = new WorkItem
        {
            Title = request.Title.Trim(),
            Status = WorkItemStatus.Backlog,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.WorkItems.Add(item);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/workitems/{item.Id}", Prioritization.ToResponse(item, now));
    }

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

        if (request.StartAtSet)
        {
            item.StartAt = request.StartAt?.ToUniversalTime();
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

        var now = DateTimeOffset.UtcNow;

        // Keep CompletedAt in sync no matter which field flipped the status to/away from
        // Done, so the Done report stays accurate even for clients that PATCH Status
        // directly instead of calling the dedicated /complete and /reopen endpoints.
        if (item.Status == WorkItemStatus.Done)
        {
            item.CompletedAt ??= now;
        }
        else
        {
            item.CompletedAt = null;
        }

        item.UpdatedAt = now;
        await db.SaveChangesAsync();

        return TypedResults.Ok(Prioritization.ToResponse(item, now));
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

        var now = DateTimeOffset.UtcNow;
        item.Status = isDone ? WorkItemStatus.Done : WorkItemStatus.Backlog;
        item.CompletedAt = isDone ? now : null;
        item.UpdatedAt = now;
        await db.SaveChangesAsync();
        return TypedResults.Ok(Prioritization.ToResponse(item, now));
    }

    /// <summary>
    /// Flips an item that is <see cref="WorkItemStatus.Waiting"/> back to actionable
    /// (Backlog) once whatever it was waiting on has happened. Meant for a separate,
    /// discreet automation to call (e.g. "has this PR been approved yet?"), distinct from
    /// the hourly reminder sweep.
    /// </summary>
    private static async Task<Results<Ok<WorkItemResponse>, NotFound, Conflict<string>>> MarkActionable(
        AppDbContext db, Guid id)
    {
        var item = await db.WorkItems.FindAsync(id);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        if (item.Status != WorkItemStatus.Waiting)
        {
            return TypedResults.Conflict($"Item is {item.Status}, not Waiting - nothing to flip.");
        }

        item.Status = WorkItemStatus.Backlog;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return TypedResults.Ok(Prioritization.ToResponse(item, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Downloadable CSV of completed items, optionally restricted to a completion-date
    /// range, for reviewing "what did I actually get done lately?" outside the app.
    /// </summary>
    private static async Task<Results<FileContentHttpResult, ValidationProblem>> DownloadDoneReport(
        AppDbContext db, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        if (from is not null && to is not null && from > to)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["from"] = ["'from' must not be after 'to'."]
            });
        }

        var query = db.WorkItems.Where(w => w.Status == WorkItemStatus.Done);
        if (from is { } fromValue)
        {
            query = query.Where(w => w.CompletedAt >= fromValue);
        }

        if (to is { } toValue)
        {
            query = query.Where(w => w.CompletedAt <= toValue);
        }

        var items = await query.OrderBy(w => w.CompletedAt).ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("Title,Reference,Priority,CreatedAt,CompletedAt");
        foreach (var item in items)
        {
            csv.AppendLine(string.Join(',',
                CsvField(item.Title),
                CsvField(item.Reference),
                item.Priority.ToString(CultureInfo.InvariantCulture),
                item.CreatedAt.ToString("O"),
                item.CompletedAt?.ToString("O") ?? ""));
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        var fileName = $"open-work-done-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv";
        return TypedResults.File(bytes, "text/csv", fileName);
    }

    private static string CsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        var escaped = value.Replace("\"", "\"\"");
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{escaped}\""
            : escaped;
    }
}
