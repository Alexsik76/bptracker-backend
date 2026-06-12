using BpTracker.Api.Data;
using BpTracker.Api.Models;
using BpTracker.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Endpoints;

public static class DevAuthEndpoints
{
    public static void MapDevAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/dev-login", async (HttpContext ctx, IAuthService auth, AppDbContext db) =>
        {
            var devUserGuid = new Guid("e6915d20-30fd-4154-b43a-85c04dbac190");
            var user = await db.Users.FindAsync(devUserGuid);
            if (user == null)
            {
                user = await db.Users.FirstOrDefaultAsync(u => u.Email == "dev@local");
                if (user == null)
                {
                    user = new User
                    {
                        Id = devUserGuid,
                        Email = "dev@local",
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                }
            }

            var token = await auth.CreateSessionAsync(user.Id);
            ctx.Response.Cookies.Append("__Host-session", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                Path = "/"
            });

            return Results.Ok(new { status = "success", email = user.Email, userId = user.Id });
        });
    }
}
