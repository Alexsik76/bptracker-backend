using BpTracker.Api.DTOs;
using BpTracker.Api.Extensions;
using BpTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BpTracker.Api.Endpoints;

public static class ReminderEndpoints
{
    public static void MapReminderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/reminders").RequireAuthorization();

        group.MapPost("/template", async (CreateTemplateDto dto, IReminderService reminder, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var template = await reminder.CreateTemplateAsync(userId.Value, dto);
            return Results.Created($"/api/v1/reminders/template/{template.Id}", template);
        });

        group.MapPatch("/template/{id:guid}", async (Guid id, UpdateTemplateDto dto, IReminderService reminder) =>
        {
            var template = await reminder.UpdateTemplateAsync(id, dto);
            return template is not null ? Results.Ok(template) : Results.NotFound();
        });

        group.MapGet("/template/active", async (IReminderService reminder, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var template = await reminder.GetActiveTemplateAsync(userId.Value);
            return template is not null ? Results.Ok(template) : Results.NotFound();
        });

        group.MapPost("/confirm", async (ConfirmIntakeDto dto, IReminderService reminder, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var report = await reminder.ConfirmAsync(userId.Value, dto.Period);
            return report is not null ? Results.Ok(report) : Results.BadRequest("No active reminder template found");
        });

        group.MapGet("/reports", async ([FromQuery] int? days, IReminderService reminder, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var count = days ?? 30;
            var reports = await reminder.GetReportsAsync(userId.Value, count);
            return Results.Ok(reports);
        });
    }
}
