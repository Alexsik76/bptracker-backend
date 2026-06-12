using System.Text.Json;
using BpTracker.Api.Data;
using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Services;

public interface ISchemaService
{
    Task<TreatmentSchema?> GetActiveAsync(Guid userId);
    Task<IReadOnlyList<TreatmentSchema>> GetAllAsync(Guid userId);
    Task<TreatmentSchema> CreateAsync(Guid userId, string doctor, DateOnly? prescribedOn, JsonDocument schedule, bool setActive);
    Task<TreatmentSchema?> UpdateAsync(Guid userId, Guid id, string doctor, DateOnly? prescribedOn, JsonDocument schedule);
    Task<bool> ActivateAsync(Guid userId, Guid id);
}

public class SchemaService(AppDbContext context) : ISchemaService
{
    public async Task<TreatmentSchema?> GetActiveAsync(Guid userId)
    {
        return await context.TreatmentSchemas
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);
    }

    public async Task<IReadOnlyList<TreatmentSchema>> GetAllAsync(Guid userId)
    {
        return await context.TreatmentSchemas
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.PrescribedOn == null ? 1 : 0)
            .ThenByDescending(s => s.PrescribedOn)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<TreatmentSchema> CreateAsync(
        Guid userId, string doctor, DateOnly? prescribedOn, JsonDocument schedule, bool setActive)
    {
        var schema = new TreatmentSchema
        {
            Doctor = doctor,
            PrescribedOn = prescribedOn ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ScheduleDocument = schedule,
            IsActive = false,
            UserId = userId
        };

        if (setActive)
        {
            await using var tx = await context.Database.BeginTransactionAsync();

            var activeSchemaIds = await context.TreatmentSchemas
                .Where(s => s.UserId == userId && s.IsActive)
                .Select(s => s.Id)
                .ToListAsync();

            if (activeSchemaIds.Count > 0)
            {
                await context.ReminderTemplates
                    .Where(t => t.UserId == userId && activeSchemaIds.Contains(t.SchemaId))
                    .ExecuteUpdateAsync(t => t.SetProperty(e => e.IsActive, false));
            }

            await context.TreatmentSchemas
                .Where(s => s.UserId == userId && s.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsActive, false));

            schema.IsActive = true;
            context.TreatmentSchemas.Add(schema);
            await context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        else
        {
            context.TreatmentSchemas.Add(schema);
            await context.SaveChangesAsync();
        }

        return schema;
    }

    public async Task<TreatmentSchema?> UpdateAsync(
        Guid userId, Guid id, string doctor, DateOnly? prescribedOn, JsonDocument schedule)
    {
        var schema = await context.TreatmentSchemas
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (schema is null)
            return null;

        schema.Doctor = doctor;
        schema.PrescribedOn = prescribedOn;
        schema.ScheduleDocument = schedule;
        await context.SaveChangesAsync();
        return schema;
    }

    public async Task<bool> ActivateAsync(Guid userId, Guid id)
    {
        if (!await context.TreatmentSchemas.AnyAsync(s => s.Id == id && s.UserId == userId))
            return false;

        await using var tx = await context.Database.BeginTransactionAsync();

        var activeSchemaIds = await context.TreatmentSchemas
            .Where(s => s.UserId == userId && s.IsActive)
            .Select(s => s.Id)
            .ToListAsync();

        if (activeSchemaIds.Count > 0)
        {
            await context.ReminderTemplates
                .Where(t => t.UserId == userId && activeSchemaIds.Contains(t.SchemaId))
                .ExecuteUpdateAsync(t => t.SetProperty(e => e.IsActive, false));
        }

        await context.TreatmentSchemas
            .Where(s => s.UserId == userId && s.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsActive, false));

        await context.TreatmentSchemas
            .Where(s => s.Id == id && s.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsActive, true));

        await tx.CommitAsync();
        return true;
    }
}
