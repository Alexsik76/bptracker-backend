using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;

namespace BpTracker.Api.Services;

public class WebPushClient : IWebPushClient
{
    private readonly PushServiceClient _client;

    public WebPushClient(HttpClient httpClient, VapidAuthentication vapidAuthentication)
    {
        _client = new PushServiceClient(httpClient)
        {
            DefaultAuthentication = vapidAuthentication
        };
    }

    public bool IsPushEnabled => true;

    public Task RequestPushMessageDeliveryAsync(PushSubscription subscription, PushMessage message, CancellationToken cancellationToken = default)
    {
        return _client.RequestPushMessageDeliveryAsync(subscription, message, cancellationToken);
    }
}
