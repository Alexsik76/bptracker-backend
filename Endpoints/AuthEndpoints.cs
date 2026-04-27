using System.Text;
using BpTracker.Api.DTOs;
using BpTracker.Api.Models;
using BpTracker.Api.Services;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace BpTracker.Api.Endpoints;

public static class AuthEndpoints
{
    private const string SessionKeyOptions = "fido2.options";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapGet("/me", async (HttpContext ctx, IAuthService auth) =>
        {
            var token = ctx.Request.Cookies["__Host-session"];
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var user = await auth.GetUserBySessionTokenAsync(token);
            return user != null 
                ? Results.Ok(new { id = user.Id, email = user.Email }) 
                : Results.Unauthorized();
        });

        group.MapPost("/logout", async (HttpContext ctx, IAuthService auth) =>
        {
            var token = ctx.Request.Cookies["__Host-session"];
            if (!string.IsNullOrEmpty(token))
            {
                await auth.InvalidateSessionAsync(token);
                ctx.Response.Cookies.Delete("__Host-session");
            }
            return Results.NoContent();
        });

        // Passkey Registration
        group.MapPost("/passkey/register/begin", async (PasskeyRegisterBeginDto dto, IFido2 fido2, IAuthService auth, HttpContext ctx) =>
        {
            var user = await auth.GetUserByEmailAsync(dto.Email) ?? await auth.CreateUserAsync(dto.Email);
            
            var existingCredentials = user.Credentials.Select(c => new PublicKeyCredentialDescriptor(c.CredentialId)).ToList();
            
            var fidoUser = new Fido2User
            {
                DisplayName = user.Email,
                Name = user.Email,
                Id = user.Id.ToByteArray()
            };

            var options = fido2.RequestNewCredential(new RequestNewCredentialParams
            {
                User = fidoUser,
                ExcludeCredentials = existingCredentials,
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    UserVerification = UserVerificationRequirement.Required,
                    ResidentKey = ResidentKeyRequirement.Required
                },
                AttestationPreference = AttestationConveyancePreference.None
            });
            
            ctx.Session.SetString(SessionKeyOptions, options.ToJson());
            return Results.Ok(options);
        }).RequireRateLimiting("auth-challenge");

        group.MapPost("/passkey/register/complete", async ([FromBody] AuthenticatorAttestationRawResponse attestationResponse, IFido2 fido2, IAuthService auth, HttpContext ctx) =>
        {
            var json = ctx.Session.GetString(SessionKeyOptions);
            if (string.IsNullOrEmpty(json)) return Results.BadRequest("Session options not found");
            
            var options = CredentialCreateOptions.FromJson(json);
            
            var success = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = attestationResponse,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = async (args, ct) =>
                {
                    var exists = await auth.GetCredentialByIdAsync(args.CredentialId);
                    return exists == null;
                }
            });

            var user = await auth.GetUserByEmailAsync(options.User.Name);
            if (user == null) return Results.BadRequest("User not found");

            var credential = new UserCredential
            {
                CredentialId = success.Id,
                PublicKey = success.PublicKey,
                SignCount = success.SignCount,
                DeviceName = "Unknown Device",
                CreatedAt = DateTime.UtcNow
            };

            await auth.AddCredentialAsync(user, credential);
            
            await SignInUserAsync(ctx, auth, user.Id);
            return Results.Ok(success);
        });

        // Passkey Login
        group.MapPost("/login/begin", (PasskeyLoginBeginDto dto, IFido2 fido2, HttpContext ctx) =>
        {
            var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = new List<PublicKeyCredentialDescriptor>(),
                UserVerification = UserVerificationRequirement.Required
            });
            ctx.Session.SetString(SessionKeyOptions, options.ToJson());
            return Results.Ok(options);
        }).RequireRateLimiting("auth-challenge");

        group.MapPost("/login/complete", async ([FromBody] AuthenticatorAssertionRawResponse clientResponse, IFido2 fido2, IAuthService auth, HttpContext ctx) =>
        {
            var json = ctx.Session.GetString(SessionKeyOptions);
            if (string.IsNullOrEmpty(json)) return Results.BadRequest("Session options not found");
            
            var options = AssertionOptions.FromJson(json);
            
            var credential = await auth.GetCredentialByIdAsync(clientResponse.RawId);
            if (credential == null) return Results.BadRequest("Unknown credential");

            var success = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = clientResponse,
                OriginalOptions = options,
                StoredPublicKey = credential.PublicKey,
                StoredSignatureCounter = credential.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = (args, ct) =>
                {
                    var userId = new Guid(args.UserHandle);
                    return Task.FromResult(credential.UserId == userId);
                }
            });

            await auth.UpdateCredentialSignCountAsync(credential.CredentialId, success.SignCount);
            
            await SignInUserAsync(ctx, auth, credential.UserId);
            return Results.Ok(success);
        });

        // Magic Link
        group.MapPost("/magic-link/request", async (MagicLinkRequestDto dto, IAuthService auth, IEmailSender emailSender, IConfiguration config, ILogger<AuthEndpointsLogger> logger) =>
        {
            // Rate limiting check as per plan_status.md
            var canRequest = await auth.CanRequestMagicLinkAsync(dto.Email);
            if (!canRequest) return Results.StatusCode(StatusCodes.Status429TooManyRequests);

            var token = await auth.CreateMagicLinkAsync(dto.Email);
            
            var appUrl = config["APP_URL"]?.TrimEnd('/') ?? "https://bptracker.home.vn.ua";
            var consumeUrl = $"{appUrl}/login?token={token}";

            try
            {
                await emailSender.SendAsync(
                    dto.Email,
                    "Ваше посилання для входу в BP Tracker",
                    $"Перейдіть за посиланням для входу (дійсне 15 хвилин):\n\n{consumeUrl}\n\nЯкщо ви не запитували це посилання — просто ігноруйте цей лист.",
                    [],
                    default);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send magic link to {Email}", dto.Email);
                // Even if sending fails, we return Accepted because it might be in outbox
            }

            return Results.Accepted();
        });

        // New: token in POST body — keeps it out of server access logs
        group.MapPost("/magic-link/consume", async (MagicLinkConsumeRequest body, IAuthService auth, HttpContext ctx) =>
        {
            var email = await auth.ConsumeMagicLinkAsync(body.Token);
            if (email == null) return Results.BadRequest("Invalid or expired magic link");

            var user = await auth.GetUserByEmailAsync(email) ?? await auth.CreateUserAsync(email);

            await SignInUserAsync(ctx, auth, user.Id);

            return Results.Ok(new { status = "success", email = user.Email });
        });

        // Deprecated stub — kept for in-flight emails already sent with old links
        group.MapGet("/consume", () =>
            Results.Json(new ProblemDetails
            {
                Status = StatusCodes.Status410Gone,
                Title = "Endpoint removed",
                Detail = "Use POST /api/v1/auth/magic-link/consume with { \"token\": \"...\" } in the request body."
            }, statusCode: StatusCodes.Status410Gone));
    }

    private static async Task SignInUserAsync(HttpContext ctx, IAuthService auth, Guid userId)
    {
        var token = await auth.CreateSessionAsync(userId);
        ctx.Response.Cookies.Append("__Host-session", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/"
        });
    }
}

public class AuthEndpointsLogger { }
