using System.Security.Claims;
using BpTracker.Api.Data;
using BpTracker.Api.DTOs;
using BpTracker.Api.Models;

namespace BpTracker.Api.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/settings").RequireAuthorization();

        group.MapGet("/", async (HttpContext ctx, AppDbContext db) =>
        {
            var userId = Guid.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var settings = await db.UserSettings.FindAsync(userId) ?? await CreateDefaultsAsync(db, userId);
            return Results.Ok(ToDto(settings));
        });

        group.MapPatch("/", async (PatchSettingsDto dto, HttpContext ctx, AppDbContext db) =>
        {
            var userId = Guid.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var settings = await db.UserSettings.FindAsync(userId) ?? await CreateDefaultsAsync(db, userId);

            if (dto.GeminiUrl is not null)
            {
                if (dto.GeminiUrl.Length > 0 && !Uri.TryCreate(dto.GeminiUrl, UriKind.Absolute, out _))
                    return Results.BadRequest(new { error = "GeminiUrl має бути валідним абсолютним URL" });
                settings.GeminiUrl = dto.GeminiUrl.Length == 0 ? null : dto.GeminiUrl;
            }
            if (dto.ExportEmail is not null)
            {
                if (dto.ExportEmail.Length > 0 && !dto.ExportEmail.Contains('@'))
                    return Results.BadRequest(new { error = "ExportEmail має бути валідною email-адресою" });
                settings.ExportEmail = dto.ExportEmail.Length == 0 ? null : dto.ExportEmail;
            }
            if (dto.SheetsTemplateUrl is not null)
            {
                if (dto.SheetsTemplateUrl.Length > 0 && !Uri.TryCreate(dto.SheetsTemplateUrl, UriKind.Absolute, out _))
                    return Results.BadRequest(new { error = "SheetsTemplateUrl має бути валідним абсолютним URL" });
                settings.SheetsTemplateUrl = dto.SheetsTemplateUrl.Length == 0 ? null : dto.SheetsTemplateUrl;
            }

            await db.SaveChangesAsync();
            return Results.Ok(ToDto(settings));
        });
    }

    private static async Task<UserSetting> CreateDefaultsAsync(AppDbContext db, Guid userId)
    {
        var settings = new UserSetting { UserId = userId };
        db.UserSettings.Add(settings);
        await db.SaveChangesAsync();
        return settings;
    }

    private static SettingsDto ToDto(UserSetting s) =>
        new(s.GeminiUrl, s.ExportEmail, s.SheetsTemplateUrl);
}
