using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BpTracker.Api.Data;
using BpTracker.Api.DTOs;
using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Services;

public class ReminderService : IReminderService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;
    public const string DefaultTimeZone = "Europe/Kyiv";

    public ReminderService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<ReminderTemplate> CreateTemplateAsync(Guid userId, CreateTemplateDto dto)
    {
        var jsonDoc = JsonDocument.Parse(dto.Periods.GetRawText());

        var template = new ReminderTemplate
        {
            SchemaId = dto.SchemaId,
            Periods = jsonDoc,
            DurationMinutes = dto.DurationMinutes,
            MaxReminders = dto.MaxReminders,
            IsActive = dto.IsActive,
            UserId = userId
        };

        if (dto.IsActive)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            await _db.ReminderTemplates
                .Where(t => t.UserId == userId && t.IsActive)
                .ExecuteUpdateAsync(t => t.SetProperty(e => e.IsActive, false));

            _db.ReminderTemplates.Add(template);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        else
        {
            _db.ReminderTemplates.Add(template);
            await _db.SaveChangesAsync();
        }

        return template;
    }

    public async Task<ReminderTemplate?> UpdateTemplateAsync(Guid userId, Guid id, UpdateTemplateDto dto)
    {
        var template = await _db.ReminderTemplates
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (template == null) return null;

        await using var tx = await _db.Database.BeginTransactionAsync();

        if (dto.IsActive.HasValue)
        {
            if (dto.IsActive.Value)
            {
                await _db.ReminderTemplates
                    .Where(t => t.UserId == userId && t.IsActive && t.Id != id)
                    .ExecuteUpdateAsync(t => t.SetProperty(e => e.IsActive, false));
            }
            template.IsActive = dto.IsActive.Value;
        }

        if (dto.Periods.HasValue)
        {
            template.Periods = JsonDocument.Parse(dto.Periods.Value.GetRawText());
        }

        if (dto.DurationMinutes.HasValue)
        {
            template.DurationMinutes = dto.DurationMinutes.Value;
        }

        if (dto.MaxReminders.HasValue)
        {
            template.MaxReminders = dto.MaxReminders.Value;
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return template;
    }

    public async Task<ReminderTemplate?> GetActiveTemplateAsync(Guid userId)
    {
        return await _db.ReminderTemplates
            .Include(t => t.Schema)
            .FirstOrDefaultAsync(t => t.UserId == userId && t.IsActive);
    }

    public async Task<IntakeReport?> ConfirmAsync(Guid userId, string period, string? timezone = null)
    {
        var activeTemplate = await GetActiveTemplateAsync(userId);
        if (activeTemplate == null) return null;

        var today = GetTodayInTimezone(timezone);

        var existing = await _db.IntakeReports
            .FirstOrDefaultAsync(r => r.TemplateId == activeTemplate.Id && r.UserId == userId && r.Period == period && r.Date == today);

        if (existing != null)
        {
            if (existing.Status != IntakeStatus.Confirmed)
            {
                existing.Status = IntakeStatus.Confirmed;
                existing.Time = _timeProvider.GetUtcNow().ToUniversalTime();
                await _db.SaveChangesAsync();
            }
            return existing;
        }

        var report = new IntakeReport
        {
            TemplateId = activeTemplate.Id,
            Period = period,
            Date = today,
            Status = IntakeStatus.Confirmed,
            Time = _timeProvider.GetUtcNow().ToUniversalTime(),
            UserId = userId
        };

        _db.IntakeReports.Add(report);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            var concurrent = await _db.IntakeReports
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.TemplateId == activeTemplate.Id && r.UserId == userId && r.Period == period && r.Date == today);
            if (concurrent != null) return concurrent;
            throw;
        }

        return report;
    }

    public async Task<IReadOnlyList<IntakeReport>> GetReportsAsync(Guid userId, int days)
    {
        var cutoff = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime.AddDays(-days));
        return await _db.IntakeReports
            .Include(r => r.Template)
            .Where(r => r.UserId == userId && r.Date >= cutoff)
            .OrderByDescending(r => r.Date)
            .ThenBy(r => r.Period)
            .ToListAsync();
    }

    public async Task<TodayMedsDto> GetTodayMedsAsync(Guid userId, string? timezone)
    {
        var today = GetTodayInTimezone(timezone);

        var activeTemplate = await GetActiveTemplateAsync(userId);
        if (activeTemplate == null)
        {
            return new TodayMedsDto(today, []);
        }

        Dictionary<string, PeriodConfig>? periods;
        try
        {
            periods = JsonSerializer.Deserialize<Dictionary<string, PeriodConfig>>(
                activeTemplate.Periods.RootElement.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch
        {
            periods = null;
        }

        if (periods == null || periods.Count == 0)
        {
            return new TodayMedsDto(today, []);
        }

        var reports = await _db.IntakeReports
            .Where(r => r.TemplateId == activeTemplate.Id && r.UserId == userId && r.Date == today)
            .ToListAsync();

        var intakes = new List<TodayIntakeStatusDto>();
        foreach (var (periodName, config) in periods)
        {
            var report = reports.FirstOrDefault(r => r.Period.Equals(periodName, StringComparison.OrdinalIgnoreCase));
            intakes.Add(new TodayIntakeStatusDto(
                periodName,
                config.Time,
                config.Meds,
                report?.Status.ToString(),
                report?.Time
            ));
        }

        return new TodayMedsDto(today, intakes);
    }

    private DateOnly GetTodayInTimezone(string? timezone)
    {
        var zoneId = string.IsNullOrWhiteSpace(timezone) ? DefaultTimeZone : timezone;

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            var localTime = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), tz);
            return DateOnly.FromDateTime(localTime.DateTime);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            if (!string.IsNullOrWhiteSpace(timezone))
            {
                throw new ArgumentException($"Invalid timezone: {timezone}", nameof(timezone));
            }
            throw;
        }
    }
}
