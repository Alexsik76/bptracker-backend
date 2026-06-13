using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using BpTracker.Api.DTOs;
using BpTracker.Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BpTracker.Api.Tests.Auth;

public class NativeAuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public NativeAuthTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task BearerTokenAuth_WithValidToken_Returns200AndCorrectUser()
    {
        var email = $"native_{Guid.NewGuid():N}@example.com";
        _factory.AllowedEmails = [email];

        var (user, sessionToken) = await TestUser.CreateAsync(_factory, email);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

        var response = await client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var meJson = await response.Content.ReadFromJsonAsync<JsonElement>();
        meJson.GetProperty("id").GetString().Should().Be(user.Id.ToString());
        meJson.GetProperty("email").GetString().Should().Be(user.Email);
    }

    [Fact]
    public async Task BearerTokenAuth_WithGarbageOrExpiredToken_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "non_existent_token_garbage");

        var response = await client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NativeLoginComplete_WithMissingOrUnknownChallengeId_Returns401WithoutDetails()
    {
        var client = _factory.CreateClient();
        
        var request = new
        {
            challengeId = "non_existent_challenge_id",
            assertion = new
            {
                id = "AQID",
                rawId = "AQID",
                type = "public-key",
                response = new
                {
                    authenticatorData = "AQID",
                    clientDataJSON = "AQID",
                    signature = "AQID",
                    userHandle = "AQID"
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/v1/auth/native/login/complete", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().BeEmpty();
    }

    [Fact]
    public async Task BearerTokenAuth_Logout_InvalidatesSessionToken()
    {
        var email = $"native_logout_{Guid.NewGuid():N}@example.com";
        _factory.AllowedEmails = [email];

        var (user, sessionToken) = await TestUser.CreateAsync(_factory, email);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

        // Access works
        var meRes1 = await client.GetAsync("/api/v1/auth/me");
        meRes1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Perform logout
        var logoutRes = await client.PostAsync("/api/v1/auth/logout", null);
        logoutRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Access now fails
        var meRes2 = await client.GetAsync("/api/v1/auth/me");
        meRes2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
