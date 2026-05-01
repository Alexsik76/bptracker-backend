using System.Security.Claims;

namespace BpTracker.Api.Extensions;

public static class HttpContextExtensions
{
    private const string SessionCookieName = "__Host-session";

    public static string? GetSessionToken(this HttpContext context)
    {
        return context.Request.Cookies[SessionCookieName];
    }

    public static Guid? GetUserId(this HttpContext context)
    {
        var claimValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }
}
