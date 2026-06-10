using System.Threading;
using System.Threading.Tasks;
using Lib.Net.Http.WebPush;
using Microsoft.Extensions.Logging;

namespace BpTracker.Api.Services;

public class DisabledWebPushClient : IWebPushClient
{
    private readonly ILogger<DisabledWebPushClient> _logger;

    public DisabledWebPushClient(ILogger<DisabledWebPushClient> logger)
    {
        _logger = logger;
    }

    public bool IsPushEnabled => false;

    public Task RequestPushMessageDeliveryAsync(PushSubscription subscription, PushMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Web push notification skipped: push is disabled (missing VAPID configuration). Endpoint: {Endpoint}", subscription.Endpoint);
        return Task.CompletedTask;
    }
}
