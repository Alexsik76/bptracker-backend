using System.Security.Claims;
using BpTracker.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace BpTracker.Api.Tests.Extensions;

public class HttpContextExtensionsTests
{
    [Fact]
    public void GetSessionToken_WhenCookieExists_ReturnsToken()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "__Host-session=test-token";

        // Act
        var token = context.GetSessionToken();

        // Assert
        Assert.Equal("test-token", token);
    }

    [Fact]
    public void GetSessionToken_WhenCookieDoesNotExist_ReturnsNull()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var token = context.GetSessionToken();

        // Assert
        Assert.Null(token);
    }

    [Fact]
    public void GetUserId_WhenClaimExists_ReturnsGuid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };

        // Act
        var result = context.GetUserId();

        // Assert
        Assert.Equal(userId, result);
    }

    [Fact]
    public void GetUserId_WhenClaimDoesNotExist_ReturnsNull()
    {
        // Arrange
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

        // Act
        var result = context.GetUserId();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetUserId_WhenClaimIsInvalidGuid_ReturnsNull()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-guid") };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };

        // Act
        var result = context.GetUserId();

        // Assert
        Assert.Null(result);
    }
}
