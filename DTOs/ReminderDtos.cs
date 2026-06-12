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

public record ConfirmIntakeDto(string Period, string? Timezone = null);

public record TodayMedsDto(
    DateOnly Date,
    List<TodayIntakeStatusDto> Intakes
);

public record TodayIntakeStatusDto(
    string Period,
    string Time,
    List<string> Meds,
    string? Status, // "Confirmed", "Missed", or null (pending)
    DateTimeOffset? TimeTaken
);
