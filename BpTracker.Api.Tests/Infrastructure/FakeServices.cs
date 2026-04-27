using System.Collections.Concurrent;
using BpTracker.Api.DTOs;
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
    public Task<ImageAnalysisResultDto> AnalyzeImageAsync(byte[] imageBytes, string mimeType)
        => Task.FromResult(new ImageAnalysisResultDto(120, 80, 70));
}
