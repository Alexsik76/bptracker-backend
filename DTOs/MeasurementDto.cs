namespace BpTracker.Api.DTOs;

public record MeasurementDto(Guid Id, DateTime RecordedAt, int Sys, int Dia, int Pulse);

public record CreateMeasurementDto(int Sys, int Dia, int Pulse);
