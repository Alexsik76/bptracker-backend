using BpTracker.Api.DTOs;

namespace BpTracker.Api.Services;

public interface IGeminiService
{
    Task<ImageAnalysisResultDto> AnalyzeImageAsync(byte[] imageBytes, string mimeType);
}
