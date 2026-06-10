using BpTracker.Api.DTOs;
using BpTracker.Api.Extensions;
using BpTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace BpTracker.Api.Endpoints;

public static class PushEndpoints
{
    public static void MapPushEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/push");

        group.MapGet("/vapid-public-key", (IConfiguration config) =>
        {
            var key = config["VAPID_PUBLIC_KEY"] ?? string.Empty;
            return Results.Ok(new { publicKey = key });
        });

        group.MapPost("/subscribe", async (PushSubscribeDto dto, IPushService push, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            await push.SaveSubscriptionAsync(userId.Value, dto);
            return Results.Ok(new { status = "success" });
        }).RequireAuthorization();

        group.MapPost("/unsubscribe", async (PushUnsubscribeDto dto, IPushService push, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            await push.RemoveSubscriptionAsync(userId.Value, dto.Endpoint);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapPost("/test", async (IPushService push, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var (sent, failed) = await push.SendToUserAsync(userId.Value, "BP Tracker", "test notification");
            return Results.Ok(new { sent, failed });
        }).RequireAuthorization();
    }
}
