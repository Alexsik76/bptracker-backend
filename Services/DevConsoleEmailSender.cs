using BpTracker.Api.Models;

namespace BpTracker.Api.Services;

public class DevConsoleEmailSender : IEmailSender
{
    private readonly ILogger<DevConsoleEmailSender> _logger;

    public DevConsoleEmailSender(ILogger<DevConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken ct = default)
    {
        var url = ExtractFirstUrl(body);
        if (url is not null)
            _logger.LogInformation("[DEV] Magic link for {To}: {Url}", to, url);
        else
            _logger.LogInformation("[DEV] Email to {To} | Subject: {Subject}\n{Body}", to, subject, body);

        return Task.CompletedTask;
    }

    private static string? ExtractFirstUrl(string body)
    {
        var start = body.IndexOf("http", StringComparison.Ordinal);
        if (start < 0) return null;

        var end = body.IndexOfAny([' ', '\r', '\n', '"', '<'], start);
        return end < 0 ? body[start..] : body[start..end];
    }
}
