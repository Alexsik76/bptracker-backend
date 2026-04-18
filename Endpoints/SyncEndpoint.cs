using BpTracker.Api.Data;
using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BpTracker.Api.Endpoints;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sync/google-sheets", async (
            AppDbContext db, 
            IHttpClientFactory httpClientFactory, 
            IOptions<GoogleSheetsSettings> settings) =>
        {
            var url = settings.Value.ScriptUrl;
            if (string.IsNullOrEmpty(url))
            {
                return Results.BadRequest("Google Script URL is not configured");
            }

            var measurements = await db.Measurements
                .OrderBy(m => m.RecordedAt)
                .ToListAsync();

            var payload = measurements.Select(m => new {
                id = m.Id,
                recordedAt = m.RecordedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                sys = m.Sys,
                dia = m.Dia,
                pulse = m.Pulse
            }).ToList();

            try 
            {
                var httpClient = httpClientFactory.CreateClient();
                var response = await httpClient.PostAsJsonAsync(url, payload);
                var result = await response.Content.ReadAsStringAsync();

                return response.IsSuccessStatusCode 
                    ? Results.Ok(result) 
                    : Results.StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });
    }
}