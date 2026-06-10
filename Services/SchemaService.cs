using System.Text.Json;
using BpTracker.Api.Data;
using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Services;

public interface ISchemaService
{
    Task<TreatmentSchema?> GetActiveAsync();
    Task<IReadOnlyList<TreatmentSchema>> GetAllAsync();
    Task<TreatmentSchema> CreateAsync(string doctor, DateOnly? prescribedOn, JsonDocument schedule, bool setActive);
    Task<TreatmentSchema?> UpdateAsync(Guid id, string doctor, DateOnly? prescribedOn, JsonDocument schedule);
    Task<bool> ActivateAsync(Guid id);
}

public class SchemaService(AppDbContext context) : ISchemaService
{
    public async Task<TreatmentSchema?> GetActiveAsync()
    {
        return await context.TreatmentSchemas
            .FirstOrDefaultAsync(s => s.IsActive);
    }

    public async Task<IReadOnlyList<TreatmentSchema>> GetAllAsync()
    {
        return await context.TreatmentSchemas
            .OrderBy(s => s.PrescribedOn == null ? 1 : 0)
            .ThenByDescending(s => s.PrescribedOn)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<TreatmentSchema> CreateAsync(
        string doctor, DateOnly? prescribedOn, JsonDocument schedule, bool setActive)
    {
        var schema = new TreatmentSchema
        {
            Doctor = doctor,
            PrescribedOn = prescribedOn ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ScheduleDocument = schedule,
            IsActive = false
        };

        if (setActive)
        {
            await using var tx = await context.Database.BeginTransactionAsync();

            var activeSchemaIds = await context.TreatmentSchemas
                .Where(s => s.IsActive)
                .Select(s => s.Id)
                .ToListAsync();

            if (activeSchemaIds.Count > 0)
            {
                await context.ReminderTemplates
                    .Where(t => activeSchemaIds.Contains(t.SchemaId))
                    .ExecuteUpdateAsync(t => t.SetProperty(e => e.IsActive, false));
            }

            await context.TreatmentSchemas
                .Where(s => s.IsActive)
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
        Guid id, string doctor, DateOnly? prescribedOn, JsonDocument schedule)
    {
        var schema = await context.TreatmentSchemas.FindAsync(id);
        if (schema is null)
            return null;

        schema.Doctor = doctor;
        schema.PrescribedOn = prescribedOn;
        schema.ScheduleDocument = schedule;
        await context.SaveChangesAsync();
        return schema;
    }

    public async Task<bool> ActivateAsync(Guid id)
    {
        if (!await context.TreatmentSchemas.AnyAsync(s => s.Id == id))
            return false;

        await using var tx = await context.Database.BeginTransactionAsync();

        var activeSchemaIds = await context.TreatmentSchemas
            .Where(s => s.IsActive)
            .Select(s => s.Id)
            .ToListAsync();

        if (activeSchemaIds.Count > 0)
        {
            await context.ReminderTemplates
                .Where(t => activeSchemaIds.Contains(t.SchemaId))
                .ExecuteUpdateAsync(t => t.SetProperty(e => e.IsActive, false));
        }

        await context.TreatmentSchemas
            .Where(s => s.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsActive, false));

        await context.TreatmentSchemas
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsActive, true));

        await tx.CommitAsync();
        return true;
    }
}
