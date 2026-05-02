using System.Security.Claims;
using BpTracker.Api.DTOs;
using BpTracker.Api.Services;

namespace BpTracker.Api.Endpoints;

public static class MeasurementEndpoints
{
    public static void MapMeasurementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/measurements").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, IMeasurementService service, int days = 90) =>
        {
            days = Math.Clamp(days, 1, 365);
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Results.Ok(await service.GetRecentAsync(userId, days));
        });

        group.MapPost("/", async (ClaimsPrincipal user, CreateMeasurementDto dto, IMeasurementService service) =>
        {
            if (dto.Sys is < 40 or > 300 || dto.Dia is < 20 or > 200 || dto.Pulse is < 30 or > 250)
                return Results.BadRequest(new { error = "Values out of valid range" });
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await service.CreateAsync(userId, dto);
            return Results.Created($"/api/v1/measurements/{result.Id}", result);
        });

        group.MapPost("/with-photo", async (
            HttpContext ctx,
            IMeasurementService service,
            IPhotoApiService photoApi) =>
        {
            if (!ctx.Request.HasFormContentType)
                return Results.BadRequest(new { error = "Expected multipart/form-data" });

            var form = await ctx.Request.ReadFormAsync();

            if (!int.TryParse(form["sys"], out var sys) ||
                !int.TryParse(form["dia"], out var dia) ||
                !int.TryParse(form["pulse"], out var pulse))
            {
                return Results.BadRequest(new { error = "Invalid measurement values" });
            }

            if (sys is < 40 or > 300 || dia is < 20 or > 200 || pulse is < 30 or > 250)
                return Results.BadRequest(new { error = "Values out of valid range" });

            (int Sys, int Dia, int Pulse)? geminiResult = null;
            if (int.TryParse(form["geminiSys"], out var gSys) &&
                int.TryParse(form["geminiDia"], out var gDia) &&
                int.TryParse(form["geminiPulse"], out var gPulse))
            {
                geminiResult = (gSys, gDia, gPulse);
            }

            var file = form.Files.GetFile("image");
            if (file is null)
                return Results.BadRequest(new { error = "Image file is missing" });

            var userId = Guid.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var dto = new CreateMeasurementDto(sys, dia, pulse);
            var result = await service.CreateAsync(userId, dto);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var imageBytes = ms.ToArray();

            // Fire-and-forget: do not await this task
            _ = photoApi.UploadAsync(imageBytes, new BpTracker.Api.Models.Measurement
            {
                Id = result.Id,
                RecordedAt = result.RecordedAt,
                Sys = result.Sys,
                Dia = result.Dia,
                Pulse = result.Pulse,
                UserId = userId
            }, geminiResult);

            return Results.Created($"/api/v1/measurements/{result.Id}", result);
        }).RequireRateLimiting("analyze");

        group.MapDelete("/{id:guid}", async (ClaimsPrincipal user, Guid id, IMeasurementService service) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var success = await service.DeleteAsync(userId, id);
            return success ? Results.NoContent() : Results.NotFound();
        });
    }
}
