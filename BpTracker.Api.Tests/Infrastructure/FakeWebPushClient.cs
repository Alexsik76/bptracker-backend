using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BpTracker.Api.Services;
using Lib.Net.Http.WebPush;

namespace BpTracker.Api.Tests.Infrastructure;

public class FakeWebPushClient : IWebPushClient
{
    public List<(PushSubscription Subscription, PushMessage Message)> Sent { get; } = [];

    public HttpStatusCode NextResponseStatusCode { get; set; } = HttpStatusCode.OK;

    public bool IsPushEnabled { get; set; } = true;

    public Task RequestPushMessageDeliveryAsync(PushSubscription subscription, PushMessage message, CancellationToken cancellationToken = default)
    {
        if (NextResponseStatusCode != HttpStatusCode.OK)
        {
            throw new PushServiceClientException("Fake push failure", NextResponseStatusCode);
        }

        Sent.Add((subscription, message));
        return Task.CompletedTask;
    }
}
