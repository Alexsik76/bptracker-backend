using System.Security.Claims;

namespace BpTracker.Api.Extensions;

public static class HttpContextExtensions
{
    private const string SessionCookieName = "__Host-session";

    public static string? GetSessionToken(this HttpContext context)
    {
        var cookie = context.Request.Cookies[SessionCookieName];
        if (!string.IsNullOrEmpty(cookie))
        {
            return cookie;
        }

        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }

        return null;
    }

    public static Guid? GetUserId(this HttpContext context)
    {
        var claimValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }
}
