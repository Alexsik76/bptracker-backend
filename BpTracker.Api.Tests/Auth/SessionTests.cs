using System.Net;
using BpTracker.Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BpTracker.Api.Tests.Auth;

public class SessionTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SessionTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Me_WithoutCookie_Returns401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/v1/auth/me");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_InvalidatesSession()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        // Confirm the session is active
        (await client.GetAsync("/api/v1/auth/me"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Logout
        (await client.PostAsync("/api/v1/auth/logout", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The same token must no longer be accepted
        var staleClient = _factory.CreateClient().AuthAs(token);
        (await staleClient.GetAsync("/api/v1/auth/me"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
