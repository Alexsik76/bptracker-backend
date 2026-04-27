using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BpTracker.Api.Data;
using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Endpoints;

public static class ExportEndpoints
{
    private static readonly TimeSpan ExportCooldown = TimeSpan.FromMinutes(10);

    public static void MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/export/csv", async (HttpContext ctx, AppDbContext db) =>
        {
            var userId = Guid.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var user = await db.Users.FindAsync(userId);
            if (user == null) return Results.Unauthorized();

            if (user.LastExportAt.HasValue && DateTime.UtcNow - user.LastExportAt.Value < ExportCooldown)
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
            user.LastExportAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Accepted(value: new { message = "Експорт у черзі", email = settings.ExportEmail });
        }).RequireAuthorization();
    }

    private static readonly TimeZoneInfo _exportTz = ResolveExportTz();

    private static TimeZoneInfo ResolveExportTz()
    {
        foreach (var id in new[] { "Europe/Kyiv", "Europe/Kiev", "FLE Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { }
        }
        Console.Error.WriteLine("[ExportEndpoints] WARNING: Kyiv timezone not found — CSV will use UTC. Install tzdata.");
        return TimeZoneInfo.Utc;
    }

    private static string BuildCsv(List<Measurement> measurements)
    {
        var sb = new StringBuilder();
        sb.AppendLine("timestamp,systolic,diastolic,pulse");
        foreach (var m in measurements)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(m.RecordedAt, _exportTz);
            sb.AppendLine(FormattableString.Invariant($"{local:yyyy-MM-dd HH:mm:ss},{m.Sys},{m.Dia},{m.Pulse}"));
        }
        return sb.ToString();
    }
}
