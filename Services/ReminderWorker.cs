using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BpTracker.Api.Data;
using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BpTracker.Api.Services;

public enum ReminderActionType
{
    None,
    SendPush,
    RecordMissed
}

public record ReminderDecision(
    ReminderActionType Action,
    DateTimeOffset? MissedReportTime,
    int? ReminderIndex
);

public class PeriodConfig
{
    public string Time { get; set; } = string.Empty;
    public List<string> Meds { get; set; } = new();
}

public class ReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderWorker> _logger;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeZoneInfo KyivTz = ResolveKyivTz();

    public ReminderWorker(IServiceScopeFactory scopeFactory, ILogger<ReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTickAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in ReminderWorker");
            }
            await Task.Delay(TickInterval, stoppingToken);
        }
    }

    public async Task ProcessTickAsync(DateTimeOffset now, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pushService = scope.ServiceProvider.GetRequiredService<IPushService>();

        var activeTemplate = await db.ReminderTemplates
            .FirstOrDefaultAsync(t => t.IsActive, ct);

        if (activeTemplate == null)
        {
            return;
        }

        Dictionary<string, PeriodConfig>? periods;
        try
        {
            periods = JsonSerializer.Deserialize<Dictionary<string, PeriodConfig>>(
                activeTemplate.Periods.RootElement.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize periods JSON for template {TemplateId}", activeTemplate.Id);
            return;
        }

        if (periods == null || periods.Count == 0)
        {
            return;
        }

        var nowLocal = TimeZoneInfo.ConvertTime(now, KyivTz);
        var todayLocal = DateOnly.FromDateTime(nowLocal.DateTime);

        var existingReports = await db.IntakeReports
            .Where(r => r.TemplateId == activeTemplate.Id && r.Date == todayLocal)
            .ToListAsync(ct);

        foreach (var (periodName, config) in periods)
        {
            var existingReport = existingReports.FirstOrDefault(r =>
                r.Period.Equals(periodName, StringComparison.OrdinalIgnoreCase));

            var decision = EvaluatePeriod(now, KyivTz, activeTemplate, periodName, config, existingReport);

            if (decision.Action == ReminderActionType.SendPush)
            {
                _logger.LogInformation("Sending push notification for period {Period}, reminder {Index}", periodName, decision.ReminderIndex);
                var users = await db.Users.Select(u => u.Id).ToListAsync(ct);
                var title = $"BP Tracker Reminder";
                var medsList = string.Join(", ", config.Meds);
                var body = $"Time to take your medications for {periodName}: {medsList}";

                foreach (var userId in users)
                {
                    await pushService.SendToUserAsync(
                        userId,
                        title,
                        body,
                        period: periodName,
                        date: todayLocal.ToString("yyyy-MM-dd"),
                        templateId: activeTemplate.Id.ToString()
                    );
                }
            }
            else if (decision.Action == ReminderActionType.RecordMissed)
            {
                _logger.LogInformation("Recording missed intake for period {Period} and date {Date}", periodName, todayLocal);
                var missedReport = new IntakeReport
                {
                    TemplateId = activeTemplate.Id,
                    Period = periodName,
                    Date = todayLocal,
                    Status = IntakeStatus.Missed,
                    Time = decision.MissedReportTime ?? now
                };

                try
                {
                    db.IntakeReports.Add(missedReport);
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    db.Entry(missedReport).State = EntityState.Detached;
                    _logger.LogWarning("IntakeReport for template {TemplateId}, period {Period}, date {Date} already exists.",
                        activeTemplate.Id, periodName, todayLocal);
                }
            }
        }
    }

    public static ReminderDecision EvaluatePeriod(
        DateTimeOffset now,
        TimeZoneInfo tz,
        ReminderTemplate template,
        string periodName,
        PeriodConfig config,
        IntakeReport? existingReport
    )
    {
        if (existingReport != null)
        {
            return new ReminderDecision(ReminderActionType.None, null, null);
        }

        var parts = config.Time.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var hour) || !int.TryParse(parts[1], out var minute))
        {
            return new ReminderDecision(ReminderActionType.None, null, null);
        }

        var nowLocal = TimeZoneInfo.ConvertTime(now, tz);
        var todayLocal = DateOnly.FromDateTime(nowLocal.DateTime);

        var windowStartLocal = new DateTime(todayLocal.Year, todayLocal.Month, todayLocal.Day, hour, minute, 0, DateTimeKind.Unspecified);
        var windowStartOffset = new DateTimeOffset(windowStartLocal, tz.GetUtcOffset(windowStartLocal));
        var windowEndOffset = windowStartOffset.AddMinutes(template.DurationMinutes);

        if (now < windowStartOffset)
        {
            return new ReminderDecision(ReminderActionType.None, null, null);
        }

        double interval = (double)template.DurationMinutes / template.MaxReminders;

        if (now >= windowStartOffset && now <= windowEndOffset)
        {
            var elapsedNow = now - windowStartOffset;
            int dueNow = Math.Min(template.MaxReminders, (int)(elapsedNow.TotalMinutes / interval) + 1);

            var prevTime = now.AddSeconds(-60);
            int duePrev = 0;
            if (prevTime >= windowStartOffset)
            {
                var elapsedPrev = prevTime - windowStartOffset;
                duePrev = Math.Min(template.MaxReminders, (int)(elapsedPrev.TotalMinutes / interval) + 1);
            }

            if (dueNow > duePrev)
            {
                return new ReminderDecision(ReminderActionType.SendPush, null, dueNow);
            }

            return new ReminderDecision(ReminderActionType.None, null, null);
        }

        var lastReminderTime = windowStartOffset.AddMinutes((template.MaxReminders - 1) * interval);
        return new ReminderDecision(ReminderActionType.RecordMissed, lastReminderTime, null);
    }

    private static TimeZoneInfo ResolveKyivTz()
    {
        foreach (var id in new[] { "Europe/Kyiv", "Europe/Kiev", "FLE Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch
            {
                // Ignore and try the next candidate
            }
        }
        return TimeZoneInfo.Utc;
    }
}
