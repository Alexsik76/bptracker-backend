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

    public ReminderService(AppDbContext db)
    {
        _db = db;
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
            IsActive = dto.IsActive
        };

        if (dto.IsActive)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            await _db.ReminderTemplates
                .Where(t => t.IsActive)
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

    public async Task<ReminderTemplate?> UpdateTemplateAsync(Guid id, UpdateTemplateDto dto)
    {
        var template = await _db.ReminderTemplates.FindAsync(id);
        if (template == null) return null;

        await using var tx = await _db.Database.BeginTransactionAsync();

        if (dto.IsActive.HasValue)
        {
            if (dto.IsActive.Value)
            {
                await _db.ReminderTemplates
                    .Where(t => t.IsActive && t.Id != id)
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
            .FirstOrDefaultAsync(t => t.IsActive);
    }

    public async Task<IntakeReport?> ConfirmAsync(Guid userId, string period)
    {
        var activeTemplate = await GetActiveTemplateAsync(userId);
        if (activeTemplate == null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await _db.IntakeReports
            .FirstOrDefaultAsync(r => r.TemplateId == activeTemplate.Id && r.Period == period && r.Date == today);

        if (existing != null)
        {
            return existing;
        }

        var report = new IntakeReport
        {
            TemplateId = activeTemplate.Id,
            Period = period,
            Date = today,
            Status = IntakeStatus.Confirmed,
            Time = DateTimeOffset.UtcNow
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
                .FirstOrDefaultAsync(r => r.TemplateId == activeTemplate.Id && r.Period == period && r.Date == today);
            if (concurrent != null) return concurrent;
            throw;
        }

        return report;
    }

    public async Task<IReadOnlyList<IntakeReport>> GetReportsAsync(Guid userId, int days)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
        return await _db.IntakeReports
            .Include(r => r.Template)
            .Where(r => r.Date >= cutoff)
            .OrderByDescending(r => r.Date)
            .ThenBy(r => r.Period)
            .ToListAsync();
    }
}
