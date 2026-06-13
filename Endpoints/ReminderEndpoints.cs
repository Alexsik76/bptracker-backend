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

            try
            {
                var template = await reminder.CreateTemplateAsync(userId.Value, dto);
                return Results.Created($"/api/v1/reminders/template/{template.Id}", template);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPatch("/template/{id:guid}", async (Guid id, UpdateTemplateDto dto, IReminderService reminder, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            try
            {
                var template = await reminder.UpdateTemplateAsync(userId.Value, id, dto);
                return template is not null ? Results.Ok(template) : Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
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

            try
            {
                var report = await reminder.ConfirmAsync(userId.Value, dto.Period, dto.Timezone);
                return report is not null ? Results.Ok(report) : Results.BadRequest("No active reminder template found");
            }
            catch (ArgumentException ex) when (ex.ParamName == "timezone")
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/today", async ([FromQuery] string? timezone, IReminderService reminder, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            try
            {
                var result = await reminder.GetTodayMedsAsync(userId.Value, timezone);
                return Results.Ok(result);
            }
            catch (ArgumentException ex) when (ex.ParamName == "timezone")
            {
                return Results.BadRequest(new { error = ex.Message });
            }
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
