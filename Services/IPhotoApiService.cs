using BpTracker.Api.DTOs;
using BpTracker.Api.Models;

namespace BpTracker.Api.Services;

public interface IPhotoApiService
{
    Task UploadAsync(byte[] imageBytes, Measurement measurement, (int Sys, int Dia, int Pulse)? aiResult, string? sourceEngine, string? ocrMeta = null);
    Task<ImageAnalysisResultDto?> RecognizeAsync(byte[] imageBytes);
}
