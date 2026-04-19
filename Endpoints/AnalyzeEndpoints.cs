using BpTracker.Api.Services;

namespace BpTracker.Api.Endpoints;

public static class AnalyzeEndpoints
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public static void MapAnalyzeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/measurements/analyze", async (HttpContext ctx, IGeminiService gemini) =>
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

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            try
            {
                var result = await gemini.AnalyzeImageAsync(ms.ToArray(), file.ContentType);
                return Results.Ok(result);
            }
            catch (HttpRequestException ex)
            {
                return Results.Problem($"Помилка Gemini API: {ex.Message}", statusCode: 502);
            }
            catch (InvalidOperationException ex)
            {
                return Results.UnprocessableEntity(new { error = ex.Message });
            }
        });
    }
}
