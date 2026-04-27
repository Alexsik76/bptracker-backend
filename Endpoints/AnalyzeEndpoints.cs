using System.Security.Claims;
using BpTracker.Api.Data;
using BpTracker.Api.Services;

namespace BpTracker.Api.Endpoints;

public static class AnalyzeEndpoints
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public static void MapAnalyzeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/measurements/analyze", async (HttpContext ctx, IGeminiService gemini, AppDbContext db) =>
        {
            if (!ctx.Request.HasFormContentType)
                return Results.BadRequest(new { error = "Очікується multipart/form-data" });

            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files.GetFile("image");

            if (file is null)
                return Results.BadRequest(new { error = "Файл зображення відсутній" });

            if (!file.ContentType.StartsWith("image/"))
                return Results.BadRequest(new { error = "Файл повинен бути зображенням" });

            if (file.Length > MaxFileSizeBytes)
                return Results.BadRequest(new { error = "Файл перевищує 10 МБ" });

            var userId = Guid.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var userSettings = await db.UserSettings.FindAsync(userId);
            var customUrl = userSettings?.GeminiUrl;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            try
            {
                var result = await gemini.AnalyzeImageAsync(ms.ToArray(), file.ContentType, customUrl);
                return Results.Ok(result);
            }
            catch (HttpRequestException ex)
            {
                return Results.Json(new { error = $"Помилка Gemini API: {ex.Message}" }, statusCode: 502);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 422);
            }
        }).RequireRateLimiting("analyze").RequireAuthorization();
    }
}
