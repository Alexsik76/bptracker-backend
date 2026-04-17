using BpTracker.Api.DTOs;

namespace BpTracker.Api.Services;

public interface IMeasurementService
{
    Task<IEnumerable<MeasurementDto>> GetRecentAsync(int count = 30);
    Task<MeasurementDto> CreateAsync(CreateMeasurementDto dto);
    Task<bool> DeleteAsync(Guid id);
}
