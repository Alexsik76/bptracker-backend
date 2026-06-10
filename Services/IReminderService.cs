using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BpTracker.Api.DTOs;
using BpTracker.Api.Models;

namespace BpTracker.Api.Services;

public interface IReminderService
{
    Task<ReminderTemplate> CreateTemplateAsync(Guid userId, CreateTemplateDto dto);
    Task<ReminderTemplate?> UpdateTemplateAsync(Guid id, UpdateTemplateDto dto);
    Task<ReminderTemplate?> GetActiveTemplateAsync(Guid userId);
    Task<IntakeReport?> ConfirmAsync(Guid userId, string period);
    Task<IReadOnlyList<IntakeReport>> GetReportsAsync(Guid userId, int days);
}
