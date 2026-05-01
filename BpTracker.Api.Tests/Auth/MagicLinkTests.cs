using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BpTracker.Api.Data;
using BpTracker.Api.Services;
using BpTracker.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BpTracker.Api.Tests.Auth;

public class MagicLinkTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MagicLinkTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MagicLink_RequestThenConsume_SetsSession()
    {
        var client = _factory.CreateClient();
        var email = $"ml_{Guid.NewGuid():N}@example.com";

        // Request magic link
        var requestRes = await client.PostJsonAsync("/api/v1/auth/magic-link/request", new { email });
        requestRes.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Extract token from captured email
        var sent = _factory.EmailSender.Captured.First(e => e.To == email);
        var token = sent.ExtractMagicToken();

        // Consume magic link
        var consumeRes = await client.PostJsonAsync("/api/v1/auth/magic-link/consume", new { token });
        consumeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Session cookie must be present
        var sessionToken = consumeRes.ExtractSessionToken();
        sessionToken.Should().NotBeNullOrEmpty();

        // /auth/me confirms the session is valid and returns the correct user
        var meClient = _factory.CreateClient().AuthAs(sessionToken!);
        var meRes = await meClient.GetAsync("/api/v1/auth/me");
        meRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await meRes.Content.ReadFromJsonAsync<JsonElement>();
        me.GetProperty("email").GetString().Should().Be(email);
    }

    [Fact]
    public async Task MagicLink_ConsumeWithUsedToken_Returns400()
    {
        var client = _factory.CreateClient();
        var email = $"ml_used_{Guid.NewGuid():N}@example.com";

        await client.PostJsonAsync("/api/v1/auth/magic-link/request", new { email });
        var token = _factory.EmailSender.Captured.First(e => e.To == email).ExtractMagicToken();

        // First consume succeeds
        (await client.PostJsonAsync("/api/v1/auth/magic-link/consume", new { token }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Second consume with the same (now deleted) token fails
        var secondRes = await client.PostJsonAsync("/api/v1/auth/magic-link/consume", new { token });
        secondRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MagicLink_ConsumeWithExpiredToken_Returns400()
    {
        var email = $"ml_exp_{Guid.NewGuid():N}@example.com";

        // Create magic link via service then force-expire it in DB
        using var scope = _factory.Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var token = await auth.CreateMagicLinkAsync(email);
        token.Should().NotBeNullOrEmpty("CreateMagicLinkAsync should succeed for a fresh email");

        var link = await db.MagicLinks.FirstAsync(l => l.Email == email);
        link.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        // Consuming an expired link must fail
        var res = await _factory.CreateClient()
            .PostJsonAsync("/api/v1/auth/magic-link/consume", new { token });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MagicLink_RequestRateLimit_Returns429()
    {
        var client = _factory.CreateClient();
        var email = $"ml_rl_{Guid.NewGuid():N}@example.com";

        // Limit is 3 per 15 min — first 3 must succeed
        for (var i = 0; i < 3; i++)
        {
            var res = await client.PostJsonAsync("/api/v1/auth/magic-link/request", new { email });
            res.StatusCode.Should().Be(HttpStatusCode.Accepted, $"request {i + 1} should succeed");
        }

        // 4th request exceeds the limit
        var limited = await client.PostJsonAsync("/api/v1/auth/magic-link/request", new { email });
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
