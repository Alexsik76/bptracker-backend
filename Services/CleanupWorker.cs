using BpTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Services;

public class CleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CleanupWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan SessionGracePeriod = TimeSpan.FromDays(7);

    public CleanupWorker(IServiceScopeFactory scopeFactory, ILogger<CleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in CleanupWorker");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sessionCutoff = DateTime.UtcNow - SessionGracePeriod;
        var deletedSessions = await db.UserSessions
            .Where(s => s.ExpiresAt < sessionCutoff)
            .ExecuteDeleteAsync(ct);

        var deletedLinks = await db.MagicLinks
            .Where(l => l.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation(
            "Cleanup: removed {Sessions} expired sessions, {Links} expired magic links",
            deletedSessions, deletedLinks);
    }
}
