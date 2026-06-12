using System.Security.Claims;
using BpTracker.Api.Data;
using BpTracker.Api.Models;
using BpTracker.Api.Services;
using Microsoft.Extensions.Options;

namespace BpTracker.Api.Endpoints;

public static class AnalyzeEndpoints
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public static void MapAnalyzeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/measurements/analyze", async (HttpContext ctx, IGeminiService gemini, IPhotoApiService photoApi, IOptions<PhotoApiSettings> photoApiOptions, AppDbContext db, ILogger<Program> logger) =>
        {
            if (!ctx.Request.HasFormContentType)
                return Results.BadRequest(new { error = "Expected multipart/form-data" });

            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files.GetFile("image");

            if (file is null)
                return Results.BadRequest(new { error = "Image file is missing" });

            if (!file.ContentType.StartsWith("image/"))
                return Results.BadRequest(new { error = "File must be an image" });

            if (file.Length > MaxFileSizeBytes)
                return Results.BadRequest(new { error = "File exceeds 10 MB" });

            var userId = Guid.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var userSettings = await db.UserSettings.FindAsync(userId);
            var customUrl = userSettings?.GeminiUrl;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var imageBytes = ms.ToArray();

            // Try local recognition via aivm-photo-api first
            var localResult = await photoApi.RecognizeAsync(imageBytes);
            if (localResult != null)
            {
                logger.LogInformation("Image analyzed successfully via PhotoAPI for user {UserId}", userId);
                return Results.Ok(localResult);
            }

            // If local recognition is unavailable or failed, fall back to Gemini
            if (photoApiOptions.Value.Enabled)
                logger.LogWarning("Local recognition unavailable or failed, falling back to Gemini");

            try
            {
                var result = await gemini.AnalyzeImageAsync(imageBytes, file.ContentType, customUrl);
                logger.LogInformation("Image analyzed successfully via Gemini fallback for user {UserId}", userId);
                return Results.Ok(result);
            }
            catch (HttpRequestException ex)
            {
                return Results.Json(new { error = $"Gemini API error: {ex.Message}" }, statusCode: 502);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 422);
            }
        }).RequireRateLimiting("analyze").RequireAuthorization();
    }
}
