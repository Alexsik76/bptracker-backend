namespace BpTracker.Api.DTOs;

public record ImageAnalysisResultDto(int Sys, int Dia, int Pulse, string Source = "gemini_auto", double? Confidence = null);
