using BpTracker.Api.Models;
using BpTracker.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BpTracker.Api.Tests.Infrastructure;

public static class TestUser
{
    /// <summary>
    /// Creates a user with an active session directly via DI (bypasses passkey flow).
    /// </summary>
    public static async Task<(User user, string sessionToken)> CreateAsync(
        ApiFactory factory, string? email = null)
    {
        email ??= $"user_{Guid.NewGuid():N}@test.com";

        using var scope = factory.Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var user = await auth.GetUserByEmailAsync(email) ?? await auth.CreateUserAsync(email);
        var token = await auth.CreateSessionAsync(user.Id);
        return (user, token);
    }
}
