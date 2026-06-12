namespace BpTracker.Api.DTOs;

public record MeasurementDto(Guid Id, DateTimeOffset RecordedAt, int Sys, int Dia, int Pulse);

public record CreateMeasurementDto(int Sys, int Dia, int Pulse, DateTimeOffset? RecordedAt = null);
