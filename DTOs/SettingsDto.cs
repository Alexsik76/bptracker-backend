namespace BpTracker.Api.DTOs;

public record SettingsDto(string? GeminiUrl, string? ExportEmail, string? SheetsTemplateUrl, bool SendPhotos);

public record PatchSettingsDto(string? GeminiUrl, string? ExportEmail, string? SheetsTemplateUrl, bool? SendPhotos);
