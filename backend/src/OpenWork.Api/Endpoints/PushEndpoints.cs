using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using OpenWork.Api.Data;
using OpenWork.Api.Dtos;
using OpenWork.Api.Models;
using OpenWork.Api.Services;

namespace OpenWork.Api.Endpoints;

public static class PushEndpoints
{
    public static RouteGroupBuilder MapPushEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/public-key", GetPublicKey);
        group.MapPost("/subscribe", Subscribe);
        group.MapPost("/unsubscribe", Unsubscribe);
        return group;
    }

    private static async Task<Ok<string>> GetPublicKey(VapidKeyProvider vapidProvider)
    {
        var keys = await vapidProvider.GetOrCreateAsync();
        return TypedResults.Ok(keys.PublicKey);
    }

    private static async Task<Ok> Subscribe(AppDbContext db, PushSubscriptionRequest request)
    {
        var existing = await db.PushSubscriptions.FirstOrDefaultAsync(p => p.Endpoint == request.Endpoint);
        if (existing is null)
        {
            db.PushSubscriptions.Add(new PushSubscriptionRecord
            {
                Endpoint = request.Endpoint,
                P256dh = request.Keys.P256dh,
                Auth = request.Keys.Auth
            });
            await db.SaveChangesAsync();
        }

        return TypedResults.Ok();
    }

    private static async Task<Ok> Unsubscribe(AppDbContext db, PushUnsubscribeRequest request)
    {
        var existing = await db.PushSubscriptions.FirstOrDefaultAsync(p => p.Endpoint == request.Endpoint);
        if (existing is not null)
        {
            db.PushSubscriptions.Remove(existing);
            await db.SaveChangesAsync();
        }

        return TypedResults.Ok();
    }
}
