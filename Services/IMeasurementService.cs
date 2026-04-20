using BpTracker.Api.DTOs;

namespace BpTracker.Api.Services;

public interface IMeasurementService
{
    Task<IEnumerable<MeasurementDto>> GetRecentAsync(Guid userId, int count = 30);
    Task<MeasurementDto> CreateAsync(Guid userId, CreateMeasurementDto dto);
    Task<bool> DeleteAsync(Guid userId, Guid id);
}
