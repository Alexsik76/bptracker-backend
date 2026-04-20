using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BpTracker.Api.Data;
using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Endpoints;

public static class ExportEndpoints
{
    private static readonly ConcurrentDictionary<Guid, DateTime> _lastExport = new();
    private static readonly TimeSpan ExportCooldown = TimeSpan.FromMinutes(10);

    public static void MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/export/csv", async (HttpContext ctx, AppDbContext db) =>
        {
            var userId = Guid.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (_lastExport.TryGetValue(userId, out var lastTime) && DateTime.UtcNow - lastTime < ExportCooldown)
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);

            var settings = await db.UserSettings.FindAsync(userId);
            if (string.IsNullOrEmpty(settings?.ExportEmail))
                return Results.BadRequest(new { error = "Вкажіть email для експорту в налаштуваннях" });

            var measurements = await db.Measurements
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.RecordedAt)
                .ToListAsync();

            var csvBytes = Encoding.UTF8.GetBytes(BuildCsv(measurements));
            var exportDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

            db.EmailOutbox.Add(new EmailOutbox
            {
                To = settings.ExportEmail,
                Subject = $"BP Tracker — експорт від {exportDate}",
                Body = "Ваші дані з BP Tracker за весь час у форматі CSV.\n\n" +
                       "Для перегляду у зручному вигляді скопіюйте собі шаблон Google Sheets " +
                       "та імпортуйте файл через File → Import → Replace current sheet.",
                AttachmentsJson = JsonSerializer.Serialize(new[]
                {
                    new { FileName = $"bp-tracker-{exportDate}.csv", Content = Convert.ToBase64String(csvBytes), ContentType = "text/csv" }
                }),
                Status = EmailStatus.Pending,
                NextAttemptAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            _lastExport[userId] = DateTime.UtcNow;

            return Results.Accepted(value: new { message = "Експорт у черзі", email = settings.ExportEmail });
        }).RequireAuthorization();
    }

    private static string BuildCsv(List<Measurement> measurements)
    {
        var sb = new StringBuilder();
        sb.AppendLine("timestamp,systolic,diastolic,pulse");
        foreach (var m in measurements)
            sb.AppendLine(FormattableString.Invariant($"{m.RecordedAt:yyyy-MM-dd HH:mm:ss},{m.Sys},{m.Dia},{m.Pulse}"));
        return sb.ToString();
    }
}
