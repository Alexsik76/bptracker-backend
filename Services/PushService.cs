using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using BpTracker.Api.Data;
using BpTracker.Api.DTOs;
using BpTracker.Api.Models;
using Lib.Net.Http.WebPush;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DbPushSubscription = BpTracker.Api.Models.PushSubscription;
using WebPushSubscription = Lib.Net.Http.WebPush.PushSubscription;

namespace BpTracker.Api.Services;

public class PushService : IPushService
{
    private readonly AppDbContext _db;
    private readonly IWebPushClient _pushClient;
    private readonly ILogger<PushService> _logger;

    public PushService(AppDbContext db, IWebPushClient pushClient, ILogger<PushService> logger)
    {
        _db = db;
        _pushClient = pushClient;
        _logger = logger;
    }

    public async Task SaveSubscriptionAsync(Guid userId, PushSubscribeDto subscriptionDto)
    {
        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == subscriptionDto.Endpoint);

        if (existing != null)
        {
            existing.UserId = userId;
            existing.P256dh = subscriptionDto.Keys.P256dh;
            existing.Auth = subscriptionDto.Keys.Auth;
            existing.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            var subscription = new DbPushSubscription
            {
                UserId = userId,
                Endpoint = subscriptionDto.Endpoint,
                P256dh = subscriptionDto.Keys.P256dh,
                Auth = subscriptionDto.Keys.Auth
            };
            _db.PushSubscriptions.Add(subscription);
        }

        await _db.SaveChangesAsync();
    }

    public async Task RemoveSubscriptionAsync(Guid userId, string endpoint)
    {
        var subscription = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint);

        if (subscription != null)
        {
            _db.PushSubscriptions.Remove(subscription);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<(int sent, int failed)> SendToUserAsync(
        Guid userId,
        string title,
        string body,
        string? period = null,
        string? date = null,
        string? templateId = null)
    {
        if (!_pushClient.IsPushEnabled)
        {
            return (0, 0);
        }

        var subscriptions = await _db.PushSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync();

        if (subscriptions.Count == 0)
        {
            return (0, 0);
        }

        var payloadObj = new
        {
            title,
            body,
            period,
            date,
            templateId
        };
        var payloadJson = JsonSerializer.Serialize(payloadObj);

        int sent = 0;
        int failed = 0;
        var deadSubscriptions = new List<DbPushSubscription>();

        foreach (var sub in subscriptions)
        {
            var webPushSub = new WebPushSubscription
            {
                Endpoint = sub.Endpoint,
                Keys = new Dictionary<string, string>
                {
                    { "p256dh", sub.P256dh },
                    { "auth", sub.Auth }
                }
            };

            var message = new PushMessage(payloadJson);

            try
            {
                await _pushClient.RequestPushMessageDeliveryAsync(webPushSub, message);
                sent++;
            }
            catch (PushServiceClientException ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Gone)
            {
                _logger.LogInformation("Push subscription is dead (HTTP {StatusCode}). Deleting: {Endpoint}", ex.StatusCode, sub.Endpoint);
                deadSubscriptions.Add(sub);
                failed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification to subscription {Endpoint}", sub.Endpoint);
                failed++;
            }
        }

        if (deadSubscriptions.Count > 0)
        {
            _db.PushSubscriptions.RemoveRange(deadSubscriptions);
            await _db.SaveChangesAsync();
        }

        return (sent, failed);
    }
}
