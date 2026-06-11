using BpTracker.Api.DTOs;
using BpTracker.Api.Extensions;
using BpTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Lib.Net.Http.WebPush;

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

        group.MapPost("/test", async (HttpContext ctx, IPushService push) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            PushTestRequest? request = null;
            if (ctx.Request.HasJsonContentType() && ctx.Request.ContentLength.GetValueOrDefault() > 0)
            {
                try
                {
                    request = await ctx.Request.ReadFromJsonAsync<PushTestRequest>();
                }
                catch (JsonException)
                {
                    return Results.BadRequest(new { error = "Invalid JSON payload." });
                }
            }

            string title = request?.Title ?? "BP Tracker";
            string body = request?.Body ?? "test notification";
            string? period = request?.Period;
            string? date = request?.Date;
            string? templateId = request?.TemplateId;
            string? tag = request?.Tag;
            bool? renotify = request?.Renotify;

            PushMessageUrgency urgency = PushMessageUrgency.Normal;
            if (!string.IsNullOrEmpty(request?.Urgency))
            {
                if (!Enum.TryParse<PushMessageUrgency>(request.Urgency, ignoreCase: true, out urgency))
                {
                    return Results.BadRequest(new { error = $"Invalid urgency value: '{request.Urgency}'. Allowed values are VeryLow, Low, Normal, High." });
                }
            }

            int? ttl = request?.Ttl;
            if (ttl.HasValue && ttl.Value < 0)
            {
                return Results.BadRequest(new { error = "TTL must be a non-negative integer." });
            }

            var (sent, failed, subscriptions) = await push.SendCustomToUserAsync(
                userId.Value,
                title,
                body,
                urgency,
                ttl,
                period,
                date,
                templateId,
                tag,
                renotify
            );

            return Results.Ok(new
            {
                sent,
                failed,
                urgency = urgency.ToString(),
                ttl,
                subscriptions
            });
        }).RequireAuthorization();
    }
}
