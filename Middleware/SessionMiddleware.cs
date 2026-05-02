using System.Security.Claims;
using BpTracker.Api.Services;

namespace BpTracker.Api.Middleware;

public class SessionMiddleware
{
    private readonly RequestDelegate _next;

    public SessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuthService auth)
    {
        var token = context.Request.Cookies["__Host-session"];

        if (!string.IsNullOrEmpty(token))
        {
            var user = await auth.GetUserBySessionTokenAsync(token);
            if (user != null)
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("sub", user.Id.ToString())
                };

                var identity = new ClaimsIdentity(claims, "Session");
                context.User = new ClaimsPrincipal(identity);

                // For Serilog enrichment
                using (Serilog.Context.LogContext.PushProperty("user_id", user.Id))
                {
                    await _next(context);
                    return;
                }
            }
        }

        await _next(context);
    }
}
