using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using BpTracker.Api.Data;
using BpTracker.Api.DTOs;
using BpTracker.Api.Services;
using BpTracker.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BpTracker.Api.Tests.Push;

public class PushTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PushTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.WebPushClient.Sent.Clear();
        _factory.WebPushClient.NextResponseStatusCode = HttpStatusCode.OK;
    }

    [Fact]
    public async Task Subscribe_WithoutSession_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostJsonAsync("/api/v1/push/subscribe", new PushSubscribeDto(
            "https://updates.push.services.mozilla.com/wpush/v2/gAAAAA",
            new PushSubscriptionKeysDto("p256dh-key", "auth-secret")
        ));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Subscribe_WithSession_CreatesSubscription()
    {
        var email = $"push_{Guid.NewGuid():N}@example.com";
        _factory.AllowedEmails = [email];
        var (user, token) = await TestUser.CreateAsync(_factory, email);
        var client = _factory.CreateClient().AuthAs(token);

        var endpoint = $"https://updates.push.services.mozilla.com/wpush/v2/gAAAAA_{Guid.NewGuid():N}";
        var subscribeDto = new PushSubscribeDto(
            endpoint,
            new PushSubscriptionKeysDto("p256dh-key", "auth-secret")
        );

        var response = await client.PostJsonAsync("/api/v1/push/subscribe", subscribeDto);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sub = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        sub.Should().NotBeNull();
        sub!.UserId.Should().Be(user.Id);
        sub.P256dh.Should().Be("p256dh-key");
        sub.Auth.Should().Be("auth-secret");
    }

    [Fact]
    public async Task Subscribe_TwiceWithSameEndpoint_UpsertsSubscription()
    {
        var email = $"push_{Guid.NewGuid():N}@example.com";
        _factory.AllowedEmails = [email];
        var (user, token) = await TestUser.CreateAsync(_factory, email);
        var client = _factory.CreateClient().AuthAs(token);

        var endpoint = $"https://updates.push.services.mozilla.com/wpush/v2/gAAAAA_{Guid.NewGuid():N}";

        var subscribeDto1 = new PushSubscribeDto(endpoint, new PushSubscriptionKeysDto("key1", "secret1"));
        await client.PostJsonAsync("/api/v1/push/subscribe", subscribeDto1);

        var subscribeDto2 = new PushSubscribeDto(endpoint, new PushSubscriptionKeysDto("key2", "secret2"));
        var response = await client.PostJsonAsync("/api/v1/push/subscribe", subscribeDto2);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var subs = await db.PushSubscriptions.Where(s => s.Endpoint == endpoint).ToListAsync();
        subs.Should().HaveCount(1);
        subs[0].P256dh.Should().Be("key2");
        subs[0].Auth.Should().Be("secret2");
    }

    [Fact]
    public async Task Unsubscribe_RemovesSubscriptionAndIsIdempotent()
    {
        var email = $"push_{Guid.NewGuid():N}@example.com";
        _factory.AllowedEmails = [email];
        var (user, token) = await TestUser.CreateAsync(_factory, email);
        var client = _factory.CreateClient().AuthAs(token);

        var endpoint = $"https://updates.push.services.mozilla.com/wpush/v2/gAAAAA_{Guid.NewGuid():N}";

        await client.PostJsonAsync("/api/v1/push/subscribe", new PushSubscribeDto(endpoint, new PushSubscriptionKeysDto("key", "secret")));

        var response = await client.PostJsonAsync("/api/v1/push/unsubscribe", new PushUnsubscribeDto(endpoint));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exists = await db.PushSubscriptions.AnyAsync(s => s.Endpoint == endpoint);
        exists.Should().BeFalse();

        var response2 = await client.PostJsonAsync("/api/v1/push/unsubscribe", new PushUnsubscribeDto(endpoint));
        response2.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task VapidPublicKey_ReturnsConfiguredPublicKey()
    {
        var email = $"push_{Guid.NewGuid():N}@example.com";
        _factory.AllowedEmails = [email];
        var (_, token) = await TestUser.CreateAsync(_factory, email);
        var client = _factory.CreateClient().AuthAs(token);

        var response = await client.GetAsync("/api/v1/push/vapid-public-key");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("publicKey").GetString().Should().Be("test-vapid-public-key");
    }

    [Fact]
    public async Task SendTestPush_RemovesSubscriptionOn410Gone()
    {
        var email = $"push_{Guid.NewGuid():N}@example.com";
        _factory.AllowedEmails = [email];
        var (user, token) = await TestUser.CreateAsync(_factory, email);
        var client = _factory.CreateClient().AuthAs(token);

        var endpoint = $"https://updates.push.services.mozilla.com/wpush/v2/gAAAAA_{Guid.NewGuid():N}";
        await client.PostJsonAsync("/api/v1/push/subscribe", new PushSubscribeDto(endpoint, new PushSubscriptionKeysDto("key", "secret")));

        _factory.WebPushClient.NextResponseStatusCode = HttpStatusCode.Gone;

        var testRes = await client.PostAsync("/api/v1/push/test", null);
        testRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await testRes.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("sent").GetInt32().Should().Be(0);
        result.GetProperty("failed").GetInt32().Should().Be(1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exists = await db.PushSubscriptions.AnyAsync(s => s.Endpoint == endpoint);
        exists.Should().BeFalse();
    }
}
