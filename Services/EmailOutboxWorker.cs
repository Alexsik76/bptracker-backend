using System.Text.Json;
using BpTracker.Api.Data;
using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Services;

public class EmailOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailOutboxWorker> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int MaxAttempts = 10;

    public EmailOutboxWorker(IServiceScopeFactory scopeFactory, ILogger<EmailOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in EmailOutboxWorker");
            }
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<SmtpEmailSender>();

        var items = await db.EmailOutbox
            .Where(e => (e.Status == EmailStatus.Pending || e.Status == EmailStatus.Failed)
                        && e.NextAttemptAt <= DateTime.UtcNow)
            .OrderBy(e => e.NextAttemptAt)
            .Take(50)
            .ToListAsync(ct);

        if (items.Count > 0)
            _logger.LogInformation("Processing {Count} outbox emails", items.Count);

        foreach (var item in items)
        {
            try
            {
                var attachments = DeserializeAttachments(item.AttachmentsJson);
                await sender.SendAsync(item.To, item.Subject, item.Body, attachments, ct);
                item.Status = EmailStatus.Sent;
                item.Attempts++;
                _logger.LogInformation("Outbox email {Id} delivered to {To}", item.Id, item.To);
            }
            catch (Exception ex)
            {
                item.Attempts++;
                item.LastError = ex.Message;
                if (item.Attempts >= MaxAttempts)
                {
                    item.Status = EmailStatus.Dead;
                    _logger.LogError("Outbox email {Id} marked Dead after {Attempts} attempts", item.Id, item.Attempts);
                }
                else
                {
                    item.Status = EmailStatus.Failed;
                    // exponential backoff: 5m, 10m, 20m, 40m, ...
                    item.NextAttemptAt = DateTime.UtcNow.AddMinutes(5 * Math.Pow(2, item.Attempts - 1));
                    _logger.LogWarning("Outbox email {Id} failed (attempt {Attempts}), retry at {Next}",
                        item.Id, item.Attempts, item.NextAttemptAt);
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private static List<EmailAttachment> DeserializeAttachments(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        var raw = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? [];
        return raw.Select(e => new EmailAttachment(
            e.GetProperty("FileName").GetString()!,
            Convert.FromBase64String(e.GetProperty("Content").GetString()!),
            e.GetProperty("ContentType").GetString()!
        )).ToList();
    }
}
