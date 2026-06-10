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

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PasskeyRegister_WithoutSession_Returns401()
    {
        var client = _factory.CreateClient();

        var beginRes = await client.PostJsonAsync("/api/v1/auth/passkey/register/begin", new { email = "test@example.com" });
        beginRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var completeRes = await client.PostJsonAsync("/api/v1/auth/passkey/register/complete", new
        {
            id = "AQID",
            rawId = "AQID",
            type = "public-key",
            response = new
            {
                clientDataJSON = "AQID",
                attestationObject = "AQID"
            }
        });
        completeRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PasskeyRegister_WithSession_BindsToSessionUserAndIgnoresRequestBodyEmail()
    {
        var email = $"allowed_{Guid.NewGuid():N}@example.com";
        _factory.AllowedEmails = [email];

        var (user, sessionToken) = await TestUser.CreateAsync(_factory, email);
        var client = _factory.CreateClient().AuthAs(sessionToken);

        var beginRes = await client.PostJsonAsync("/api/v1/auth/passkey/register/begin", new { email = "different@example.com" });
        beginRes.StatusCode.Should().Be(HttpStatusCode.OK);

        string? aspNetSessionCookie = null;
        if (beginRes.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var cookie in cookies)
            {
                if (cookie.StartsWith(".AspNetCore.Session=", StringComparison.OrdinalIgnoreCase))
                {
                    aspNetSessionCookie = cookie.Split(';')[0].Trim();
                    break;
                }
            }
        }

        var completeClient = _factory.CreateClient();
        var cookieHeader = $"__Host-session={sessionToken}";
        if (aspNetSessionCookie != null)
        {
            cookieHeader += $"; {aspNetSessionCookie}";
        }
        completeClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);

        var completeRes = await completeClient.PostJsonAsync("/api/v1/auth/passkey/register/complete", new
        {
            id = "AQID",
            rawId = "AQID",
            type = "public-key",
            response = new
            {
                clientDataJSON = "AQID",
                attestationObject = "AQID"
            }
        });
        completeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var credentials = await db.UserCredentials
            .Where(c => c.UserId == user.Id)
            .ToListAsync();

        credentials.Should().HaveCount(1);
        credentials[0].CredentialId.Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task MagicLinkConsume_AllowedEmail_CreatesUserAndSetsSession()
    {
        var email = $"ml_allowed_{Guid.NewGuid():N}@example.com";
        _factory.AllowedEmails = [email];

        var client = _factory.CreateClient();

        await client.PostJsonAsync("/api/v1/auth/magic-link/request", new { email });
        var token = _factory.EmailSender.Captured.First(e => e.To == email).ExtractMagicToken();

        var consumeRes = await client.PostJsonAsync("/api/v1/auth/magic-link/consume", new { token });
        consumeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessionToken = consumeRes.ExtractSessionToken();
        sessionToken.Should().NotBeNullOrEmpty();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        user.Should().NotBeNull();
    }

    [Fact]
    public async Task MagicLinkConsume_NotAllowedEmail_DoesNotCreateUserOrSessionAndReturnsSameResponse()
    {
        var email = $"ml_disallowed_{Guid.NewGuid():N}@example.com";
        _factory.AllowedEmails = ["some_other_email@example.com"];

        var client = _factory.CreateClient();

        await client.PostJsonAsync("/api/v1/auth/magic-link/request", new { email });
        var token = _factory.EmailSender.Captured.First(e => e.To == email).ExtractMagicToken();

        var consumeRes = await client.PostJsonAsync("/api/v1/auth/magic-link/consume", new { token });
        consumeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessionToken = consumeRes.ExtractSessionToken();
        sessionToken.Should().BeNullOrEmpty();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        user.Should().BeNull();
    }

    [Fact]
    public async Task MagicLinkConsume_EmptyAllowedEmails_DeniesAll()
    {
        var email = $"ml_empty_{Guid.NewGuid():N}@example.com";
        _factory.AllowedEmails = [];

        var client = _factory.CreateClient();

        await client.PostJsonAsync("/api/v1/auth/magic-link/request", new { email });
        var token = _factory.EmailSender.Captured.First(e => e.To == email).ExtractMagicToken();

        var consumeRes = await client.PostJsonAsync("/api/v1/auth/magic-link/consume", new { token });
        consumeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessionToken = consumeRes.ExtractSessionToken();
        sessionToken.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task FullBootstrapPath_EndToEnd_Works()
    {
        var email = $"ml_bootstrap_{Guid.NewGuid():N}@example.com";
        _factory.AllowedEmails = [email];

        var client = _factory.CreateClient();

        var reqRes = await client.PostJsonAsync("/api/v1/auth/magic-link/request", new { email });
        reqRes.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var token = _factory.EmailSender.Captured.First(e => e.To == email).ExtractMagicToken();

        var consumeRes = await client.PostJsonAsync("/api/v1/auth/magic-link/consume", new { token });
        consumeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessionToken = consumeRes.ExtractSessionToken();
        sessionToken.Should().NotBeNullOrEmpty();

        var registerClient = _factory.CreateClient().AuthAs(sessionToken!);

        var beginRes = await registerClient.PostJsonAsync("/api/v1/auth/passkey/register/begin", new { email });
        beginRes.StatusCode.Should().Be(HttpStatusCode.OK);

        string? aspNetSessionCookie = null;
        if (beginRes.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var cookie in cookies)
            {
                if (cookie.StartsWith(".AspNetCore.Session=", StringComparison.OrdinalIgnoreCase))
                {
                    aspNetSessionCookie = cookie.Split(';')[0].Trim();
                    break;
                }
            }
        }

        var completeClient = _factory.CreateClient();
        var cookieHeader = $"__Host-session={sessionToken}";
        if (aspNetSessionCookie != null)
        {
            cookieHeader += $"; {aspNetSessionCookie}";
        }
        completeClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);

        var completeRes = await completeClient.PostJsonAsync("/api/v1/auth/passkey/register/complete", new
        {
            id = "AQIE",
            rawId = "AQIE",
            type = "public-key",
            response = new
            {
                clientDataJSON = "AQIE",
                attestationObject = "AQIE"
            }
        });
        completeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var logoutClient = _factory.CreateClient().AuthAs(sessionToken!);
        var logoutRes = await logoutClient.PostAsync("/api/v1/auth/logout", null);
        logoutRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var loginClient = _factory.CreateClient();

        var loginBeginRes = await loginClient.PostJsonAsync("/api/v1/auth/login/begin", new { email });
        loginBeginRes.StatusCode.Should().Be(HttpStatusCode.OK);

        string? loginSessionCookie = null;
        if (loginBeginRes.Headers.TryGetValues("Set-Cookie", out var loginCookies))
        {
            foreach (var cookie in loginCookies)
            {
                if (cookie.StartsWith(".AspNetCore.Session=", StringComparison.OrdinalIgnoreCase))
                {
                    loginSessionCookie = cookie.Split(';')[0].Trim();
                    break;
                }
            }
        }

        var loginCompleteClient = _factory.CreateClient();
        if (loginSessionCookie != null)
        {
            loginCompleteClient.DefaultRequestHeaders.Add("Cookie", loginSessionCookie);
        }

        var loginCompleteRes = await loginCompleteClient.PostJsonAsync("/api/v1/auth/login/complete", new
        {
            id = "AQIE",
            rawId = "AQIE",
            type = "public-key",
            response = new
            {
                authenticatorData = "AQIE",
                clientDataJSON = "AQIE",
                signature = "AQIE",
                userHandle = "AQIE"
            }
        });
        loginCompleteRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginSession = loginCompleteRes.ExtractSessionToken();
        loginSession.Should().NotBeNullOrEmpty();

        var meClient = _factory.CreateClient().AuthAs(loginSession!);
        var meRes = await meClient.GetAsync("/api/v1/auth/me");
        meRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var meJson = await meRes.Content.ReadFromJsonAsync<JsonElement>();
        meJson.GetProperty("email").GetString().Should().Be(email);
    }
}
