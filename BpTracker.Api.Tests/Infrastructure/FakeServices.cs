using System.Collections.Concurrent;
using BpTracker.Api.DTOs;
using BpTracker.Api.Models;
using BpTracker.Api.Services;

namespace BpTracker.Api.Tests.Infrastructure;

public record CapturedEmail(string To, string Subject, string Body);

public class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentBag<CapturedEmail> _captured = new();

    public IReadOnlyList<CapturedEmail> Captured => _captured.ToList();

    public Task SendAsync(string to, string subject, string body,
        IReadOnlyList<EmailAttachment> attachments, CancellationToken ct = default)
    {
        _captured.Add(new CapturedEmail(to, subject, body));
        return Task.CompletedTask;
    }
}

public class FakeGeminiService : IGeminiService
{
    public Exception? ExceptionToThrow { get; set; }

    public Task<ImageAnalysisResultDto> AnalyzeImageAsync(byte[] imageBytes, string mimeType, string? customUrl = null)
    {
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;
        return Task.FromResult(new ImageAnalysisResultDto(120, 80, 70, "gemini", null));
    }
}

public enum PhotoApiBehavior { Success, Failed }

public class FakePhotoApiService : IPhotoApiService
{
    public PhotoApiBehavior Behavior { get; set; } = PhotoApiBehavior.Failed;

    public Task UploadAsync(byte[] imageBytes, Measurement measurement, (int Sys, int Dia, int Pulse)? aiResult, string? sourceEngine)
        => Task.CompletedTask;

    public Task<ImageAnalysisResultDto?> RecognizeAsync(byte[] imageBytes)
    {
        if (Behavior == PhotoApiBehavior.Success)
            return Task.FromResult<ImageAnalysisResultDto?>(new ImageAnalysisResultDto(125, 82, 68, "local_ocr", 0.92));
        return Task.FromResult<ImageAnalysisResultDto?>(null);
    }
}
