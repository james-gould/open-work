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
        group.MapPost("/", Create);
        group.MapPatch("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/complete", Complete);
        group.MapPost("/{id:guid}/reopen", Reopen);
        return group;
    }

    private static async Task<Ok<List<WorkItemResponse>>> GetAll(AppDbContext db, bool includeDone = false)
    {
        var now = DateTimeOffset.UtcNow;
        var query = db.WorkItems.AsQueryable();
        if (!includeDone)
        {
            query = query.Where(w => !w.IsDone);
        }

        var items = await query.ToListAsync();
        var sorted = Prioritization.Sort(items, now).Select(w => Prioritization.ToResponse(w, now)).ToList();
        return TypedResults.Ok(sorted);
    }

    private static async Task<Results<Created<WorkItemResponse>, ValidationProblem>> Create(
        AppDbContext db, CreateWorkItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["title"] = ["Title is required."]
            });
        }

        var now = DateTimeOffset.UtcNow;
        var item = new WorkItem
        {
            Title = request.Title.Trim(),
            Body = request.Body,
            Priority = request.Priority,
            RequiresActionAt = request.RequiresActionAt,
            CompletionDueAt = request.CompletionDueAt,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.WorkItems.Add(item);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/workitems/{item.Id}", Prioritization.ToResponse(item, now));
    }

    private static async Task<Results<Ok<WorkItemResponse>, NotFound>> Update(
        AppDbContext db, Guid id, UpdateWorkItemRequest request)
    {
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

        if (request.Priority is { } priority)
        {
            item.Priority = priority;
        }

        if (request.RequiresActionAtSet)
        {
            item.RequiresActionAt = request.RequiresActionAt;
        }

        if (request.CompletionDueAtSet)
        {
            item.CompletionDueAt = request.CompletionDueAt;
        }

        if (request.ManualCategorySet)
        {
            item.ManualCategory = request.ManualCategory;
        }

        if (request.IsDone is { } isDone)
        {
            item.IsDone = isDone;
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

        item.IsDone = isDone;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return TypedResults.Ok(Prioritization.ToResponse(item, DateTimeOffset.UtcNow));
    }
}
