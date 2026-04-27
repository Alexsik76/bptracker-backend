using System.Security.Claims;
using BpTracker.Api.DTOs;
using BpTracker.Api.Services;

namespace BpTracker.Api.Endpoints;

public static class MeasurementEndpoints
{
    public static void MapMeasurementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/measurements").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, IMeasurementService service) => 
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Results.Ok(await service.GetRecentAsync(userId));
        });

        group.MapPost("/", async (ClaimsPrincipal user, CreateMeasurementDto dto, IMeasurementService service) =>
        {
            if (dto.Sys is < 40 or > 300 || dto.Dia is < 20 or > 200 || dto.Pulse is < 30 or > 250)
                return Results.BadRequest(new { error = "Values out of valid range" });
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await service.CreateAsync(userId, dto);
            return Results.Created($"/api/v1/measurements/{result.Id}", result);
        });

        group.MapDelete("/{id:guid}", async (ClaimsPrincipal user, Guid id, IMeasurementService service) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var success = await service.DeleteAsync(userId, id);
            return success ? Results.NoContent() : Results.NotFound();
        });
    }
}
