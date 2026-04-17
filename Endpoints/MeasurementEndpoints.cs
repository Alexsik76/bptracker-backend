using BpTracker.Api.DTOs;
using BpTracker.Api.Services;

namespace BpTracker.Api.Endpoints;

public static class MeasurementEndpoints
{
    public static void MapMeasurementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/measurements");

        group.MapGet("/", async (IMeasurementService service) => 
            Results.Ok(await service.GetRecentAsync()));

        group.MapPost("/", async (CreateMeasurementDto dto, IMeasurementService service) =>
        {
            var result = await service.CreateAsync(dto);
            return Results.Created($"/api/measurements/{result.Id}", result);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IMeasurementService service) =>
        {
            var success = await service.DeleteAsync(id);
            return success ? Results.NoContent() : Results.NotFound();
        });
    }
}
