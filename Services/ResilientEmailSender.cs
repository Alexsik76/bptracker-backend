using System.Text.Json;
using BpTracker.Api.Data;
using BpTracker.Api.Models;

namespace BpTracker.Api.Services;

// Decorator over SmtpEmailSender: 3 attempts with exponential backoff.
// On full failure saves the message to email_outbox for background retry.
public class ResilientEmailSender : IEmailSender
{
    private readonly SmtpEmailSender _inner;
    private readonly AppDbContext _db;
    private readonly ILogger<ResilientEmailSender> _logger;

    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)];

    public ResilientEmailSender(SmtpEmailSender inner, AppDbContext db, ILogger<ResilientEmailSender> logger)
    {
        _inner = inner;
        _db = db;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken ct = default)
    {
        string? lastError = null;
        for (int attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            try
            {
                await _inner.SendAsync(to, subject, body, attachments, ct);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogWarning("Email send attempt {Attempt}/{Total} failed for {To}: {Error}",
                    attempt + 1, RetryDelays.Length + 1, to, ex.Message);
                if (attempt < RetryDelays.Length)
                    await Task.Delay(RetryDelays[attempt], ct);
            }
        }

        _logger.LogError("All SMTP attempts exhausted for {To}, saving to outbox", to);
        _db.EmailOutbox.Add(new EmailOutbox
        {
            To = to,
            Subject = subject,
            Body = body,
            AttachmentsJson = SerializeAttachments(attachments),
            Status = EmailStatus.Failed,
            Attempts = RetryDelays.Length + 1,
            LastError = lastError,
            NextAttemptAt = DateTime.UtcNow.AddMinutes(5)
        });
        await _db.SaveChangesAsync(ct);
    }

    private static string? SerializeAttachments(IReadOnlyList<EmailAttachment> attachments)
    {
        if (attachments.Count == 0) return null;
        return JsonSerializer.Serialize(
            attachments.Select(a => new { a.FileName, Content = Convert.ToBase64String(a.Content), a.ContentType }));
    }
}
