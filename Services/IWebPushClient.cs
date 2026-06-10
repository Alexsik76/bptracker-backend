using System.Threading;
using System.Threading.Tasks;
using Lib.Net.Http.WebPush;

namespace BpTracker.Api.Services;

public interface IWebPushClient
{
    bool IsPushEnabled { get; }
    Task RequestPushMessageDeliveryAsync(PushSubscription subscription, PushMessage message, CancellationToken cancellationToken = default);
}
