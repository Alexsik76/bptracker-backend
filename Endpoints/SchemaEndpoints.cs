using BpTracker.Api.Services;

namespace BpTracker.Api.Endpoints;

public static class SchemaEndpoints
{
    public static void MapSchemaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/schemas");

        group.MapGet("/active", async (ISchemaService service) =>
        {
            var schema = await service.GetActiveAsync();
            return schema is not null ? Results.Ok(schema) : Results.NotFound();
        });
    }
}
