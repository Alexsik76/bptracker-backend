using BpTracker.Api.Data;
using BpTracker.Api.DTOs;
using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Services;

public class MeasurementService(AppDbContext context) : IMeasurementService
{
    public async Task<IEnumerable<MeasurementDto>> GetRecentAsync(Guid userId, int count = 30)
    {
        return await context.Measurements
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.RecordedAt)
            .Take(count)
            .Select(m => new MeasurementDto(m.Id, m.RecordedAt, m.Sys, m.Dia, m.Pulse))
            .ToListAsync();
    }

    public async Task<MeasurementDto> CreateAsync(Guid userId, CreateMeasurementDto dto)
    {
        var measurement = new Measurement
        {
            UserId = userId,
            Sys = dto.Sys,
            Dia = dto.Dia,
            Pulse = dto.Pulse
        };

        context.Measurements.Add(measurement);
        await context.SaveChangesAsync();

        return new MeasurementDto(
            measurement.Id,
            measurement.RecordedAt,
            measurement.Sys,
            measurement.Dia,
            measurement.Pulse);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id)
    {
        var measurement = await context.Measurements
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
            
        if (measurement == null)
            return false;

        context.Measurements.Remove(measurement);
        await context.SaveChangesAsync();
        
        return true;
    }
}
