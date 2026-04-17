using BpTracker.Api.Data;
using BpTracker.Api.DTOs;
using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Services;

public class MeasurementService(AppDbContext context) : IMeasurementService
{
    public async Task<IEnumerable<MeasurementDto>> GetRecentAsync(int count = 30)
    {
        return await context.Measurements
            .OrderByDescending(m => m.RecordedAt)
            .Take(count)
            .Select(m => new MeasurementDto(m.Id, m.RecordedAt, m.Sys, m.Dia, m.Pulse))
            .ToListAsync();
    }

    public async Task<MeasurementDto> CreateAsync(CreateMeasurementDto dto)
    {
        var measurement = new Measurement
        {
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
}
