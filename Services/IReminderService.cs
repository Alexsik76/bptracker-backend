using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BpTracker.Api.DTOs;
using BpTracker.Api.Models;

namespace BpTracker.Api.Services;

public interface IReminderService
{
    Task<ReminderTemplate> CreateTemplateAsync(Guid userId, CreateTemplateDto dto);
    Task<ReminderTemplate?> UpdateTemplateAsync(Guid userId, Guid id, UpdateTemplateDto dto);
    Task<ReminderTemplate?> GetActiveTemplateAsync(Guid userId);
    Task<IntakeReport?> ConfirmAsync(Guid userId, string period, string? timezone = null);
    Task<IReadOnlyList<IntakeReport>> GetReportsAsync(Guid userId, int days);
    Task<TodayMedsDto> GetTodayMedsAsync(Guid userId, string? timezone);
}
