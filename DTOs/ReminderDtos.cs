using System;
using System.Text.Json;

namespace BpTracker.Api.DTOs;

public record CreateTemplateDto(
    Guid SchemaId,
    JsonElement Periods,
    int DurationMinutes,
    int MaxReminders,
    bool IsActive
);

public record UpdateTemplateDto(
    JsonElement? Periods,
    int? DurationMinutes,
    int? MaxReminders,
    bool? IsActive
);

public record ConfirmIntakeDto(string Period);
