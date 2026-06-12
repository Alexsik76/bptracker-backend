using BpTracker.Api.Data;
using BpTracker.Api.Endpoints;
using BpTracker.Api.Services;
using BpTracker.Api.Models;
using BpTracker.Api.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Security.Claims;
using System.Text.Json;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Fido2NetLib;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using Lib.Net.Http.WebPush;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
   {
       options.ForwardedHeaders =
           Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
           Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
       options.KnownNetworks.Clear();
       options.KnownProxies.Clear();
   });

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .WriteTo.Console(new CompactJsonFormatter())
    .WriteTo.Seq(builder.Configuration["SEQ_URL"] ?? "http://seq:80")
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (builder.Configuration["UseInMemoryDatabase"] == "true")
    {
        options.UseInMemoryDatabase("InMemoryDbForTesting");
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();

var geminiHealthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
var photoApiHealthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString!)
    .AddAsyncCheck("gemini", async () =>
    {
        var apiKey = builder.Configuration["GEMINI_API_KEY"];
        var model = builder.Configuration["GEMINI_MODEL"] ?? "gemini-flash-latest";
        if (string.IsNullOrEmpty(apiKey)) return HealthCheckResult.Unhealthy("Gemini API key is missing");

        try
        {
            var response = await geminiHealthClient.GetAsync($"https://generativelanguage.googleapis.com/v1beta/models/{model}?key={apiKey}");
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Gemini API unavailable");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("Gemini API is unreachable");
        }
    })
    .AddAsyncCheck("photo-api", async () =>
    {
        var enabled = builder.Configuration["PHOTO_API_ENABLED"] == "true";
        if (!enabled) return HealthCheckResult.Healthy("aivm-photo-api is disabled");

        var url = builder.Configuration["PHOTO_API_URL"];
        if (string.IsNullOrEmpty(url))
            return HealthCheckResult.Degraded("PHOTO_API_URL is not configured");

        try
        {
            var response = await photoApiHealthClient.GetAsync($"{url.TrimEnd('/')}/health");
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded("aivm-photo-api health check failed");
        }
        catch
        {
            return HealthCheckResult.Degraded("aivm-photo-api is unreachable");
        }
    });

builder.Services.AddScoped<IMeasurementService, MeasurementService>();
builder.Services.AddScoped<ISchemaService, SchemaService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPushService, PushService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddSingleton(TimeProvider.System);

var vapidPublicKey = builder.Configuration["VAPID_PUBLIC_KEY"];
var vapidPrivateKey = builder.Configuration["VAPID_PRIVATE_KEY"];
var vapidSubject = builder.Configuration["VAPID_SUBJECT"];

if (string.IsNullOrEmpty(vapidPublicKey) || string.IsNullOrEmpty(vapidPrivateKey) || string.IsNullOrEmpty(vapidSubject))
{
    Log.Warning("VAPID configuration is incomplete. Web push sending will be disabled.");
    builder.Services.AddSingleton<IWebPushClient, DisabledWebPushClient>();
}
else
{
    builder.Services.AddSingleton(new Lib.Net.Http.WebPush.Authentication.VapidAuthentication(vapidPublicKey, vapidPrivateKey)
    {
        Subject = vapidSubject
    });
    builder.Services.AddHttpClient<IWebPushClient, WebPushClient>();
}

builder.Services.Configure<AuthSettings>(options =>
{
    var allowedEmailsStr = builder.Configuration["ALLOWED_EMAILS"];
    if (string.IsNullOrEmpty(allowedEmailsStr))
    {
        options.AllowedEmails = [];
    }
    else
    {
        options.AllowedEmails = allowedEmailsStr.Split(',').ToHashSet();
    }
});

builder.Services.Configure<SmtpSettings>(options =>
{
    options.Host = builder.Configuration["SMTP_HOST"] ?? string.Empty;
    options.Port = int.TryParse(builder.Configuration["SMTP_PORT"], out var smtpPort) ? smtpPort : 587;
    options.Username = builder.Configuration["SMTP_USER"] ?? string.Empty;
    options.Password = builder.Configuration["SMTP_PASSWORD"] ?? string.Empty;
    options.FromAddress = builder.Configuration["SMTP_FROM"] ?? string.Empty;
    options.FromName = builder.Configuration["SMTP_FROM_NAME"] ?? "BP Tracker";
    options.UseTls = builder.Configuration["SMTP_TLS"] != "false";
});
builder.Services.AddScoped<SmtpEmailSender>();
var smtpConfigured = !string.IsNullOrEmpty(builder.Configuration["SMTP_HOST"]);
if (builder.Environment.IsDevelopment() && !smtpConfigured)
    builder.Services.AddScoped<IEmailSender, DevConsoleEmailSender>();
else
    builder.Services.AddScoped<IEmailSender, ResilientEmailSender>();
builder.Services.AddHostedService<EmailOutboxWorker>();
builder.Services.AddHostedService<CleanupWorker>();
builder.Services.AddHostedService<ReminderWorker>();

builder.Services.Configure<GeminiSettings>(options =>
{
    options.ApiKey = builder.Configuration["GEMINI_API_KEY"] ?? string.Empty;
    var modelEnv = builder.Configuration["GEMINI_MODEL"];
    options.Model = string.IsNullOrWhiteSpace(modelEnv) ? "gemini-flash-latest" : modelEnv;
});
builder.Services.AddHttpClient<IGeminiService, GeminiService>();

builder.Services.Configure<PhotoApiSettings>(options =>
{
    options.Enabled = builder.Configuration["PHOTO_API_ENABLED"] == "true";
    options.Url = builder.Configuration["PHOTO_API_URL"];
    options.Token = builder.Configuration["PHOTO_API_TOKEN"];
    options.DeviceModel = builder.Configuration["PHOTO_API_DEVICE_MODEL"] ?? "Paramed Expert-X";

    if (options.Enabled)
    {
        if (string.IsNullOrEmpty(options.Url))
            throw new OptionsValidationException("PhotoApi", typeof(PhotoApiSettings), new[] { "PHOTO_API_URL is required when PHOTO_API_ENABLED is true" });
        if (string.IsNullOrEmpty(options.Token))
            throw new OptionsValidationException("PhotoApi", typeof(PhotoApiSettings), new[] { "PHOTO_API_TOKEN is required when PHOTO_API_ENABLED is true" });
    }
});
builder.Services.AddHttpClient("PhotoApi", c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<IPhotoApiService, PhotoApiService>();

builder.Services.AddScoped<IFido2, Fido2>(sp => new Fido2(new Fido2Configuration
{
    ServerDomain = builder.Configuration["FIDO2_DOMAIN"] ?? "bptracker.home.vn.ua",
    ServerName = "BP Tracker",
    Origins = (builder.Configuration["CORS_ORIGINS"] ?? "https://bptracker.home.vn.ua")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet()
}));

builder.Services.AddDistributedMemoryCache(); // For storing Fido2 challenges
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(5);
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Session";
    options.DefaultChallengeScheme = "Session";
})
.AddScheme<AuthenticationSchemeOptions, SessionAuthHandler>("Session", null);

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    // Per-user rate limit; falls back to IP if userId is unavailable (should not happen on auth'd endpoint)
    options.AddPolicy("analyze", ctx =>
    {
        var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            Log.Warning("Rate limit: userId missing on /analyze, falling back to IP");
            userId = "ip:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // Per-IP rate limit for unauthenticated Fido2 challenge endpoints
    options.AddPolicy("auth-challenge", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            "ip:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Try again later." }, token);
    };
});

var corsOrigins = (builder.Configuration["CORS_ORIGINS"] ?? "https://bptracker.home.vn.ua")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("BP Tracker API")
            .WithTheme(ScalarTheme.Moon)
            .WithDefaultHttpClient(ScalarTarget.JavaScript, ScalarClient.Fetch);
    });
    app.MapDevAuthEndpoints();
}

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionFeature?.Error;
        if (exception is null) return;

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var requestId = context.Response.Headers["X-Request-ID"].FirstOrDefault()
                        ?? context.TraceIdentifier;

        logger.LogError(exception,
            "Unhandled exception [{Method} {Path}] request_id={RequestId}",
            context.Request.Method, context.Request.Path, requestId);

        (int status, string title) = exception switch
        {
            DbUpdateConcurrencyException => (409, "Conflict"),
            DbUpdateException dbEx when IsUniqueConstraintViolation(dbEx) => (409, "Conflict"),
            DbUpdateException => (500, "Database error"),
            _ => (500, "An unexpected error occurred")
        };

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            type = $"https://httpstatuses.com/{status}",
            title,
            status,
            traceId = requestId,
            detail = app.Environment.IsProduction() ? null : exception.ToString()
        });
    });
});

// CORS must be before auth/session middleware to handle OPTIONS preflight
app.UseCors();

app.Use(async (context, next) =>
{
    var requestId = context.Request.Headers["X-Request-ID"].FirstOrDefault()
                    ?? Guid.NewGuid().ToString();
    using (Serilog.Context.LogContext.PushProperty("request_id", requestId))
    {
        context.Response.Headers["X-Request-ID"] = requestId;
        await next();
    }
});

app.UseSerilogRequestLogging();

// ASP.NET session (used by Fido2 for challenge storage) must come before endpoint handlers
app.UseSession();

// Custom session middleware: reads __Host-session cookie, sets HttpContext.User
// Must run before UseRateLimiter so the "analyze" policy can partition by userId
app.UseMiddleware<SessionMiddleware>();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/api/v1/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapMeasurementEndpoints();
app.MapSchemaEndpoints();
app.MapAnalyzeEndpoints();
app.MapAuthEndpoints();
app.MapSettingsEndpoints();
app.MapExportEndpoints();
app.MapPushEndpoints();
app.MapReminderEndpoints();

// Apply migrations on startup with retry (waits for DB container to be ready)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var db = services.GetRequiredService<AppDbContext>();

    const int maxAttempts = 15;
    for (int i = 0; i < maxAttempts; i++)
    {
        try
        {
            if (db.Database.IsNpgsql())
            {
                db.Database.Migrate();
                logger.LogInformation("Database migrated successfully");
            }
            else
            {
                logger.LogInformation("Skipping migrations for non-Npgsql provider: {Provider}", db.Database.ProviderName);
            }
            break;
        }
        catch (Exception ex)
        {
            if (i == maxAttempts - 1) throw;
            logger.LogWarning(ex, "Database not ready, retrying in 2 s (attempt {Attempt}/{Max})", i + 1, maxAttempts);
            Thread.Sleep(2000);
        }
    }

    if (app.Environment.IsDevelopment())
    {
        try
        {
            await DbInitializer.SeedAsync(db);
            logger.LogInformation("Development database seeded successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the development database");
        }
    }
}

app.Run();

static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
    ex.InnerException?.Message.Contains("23505") == true ||
    ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;

public partial class Program { }

public class SessionAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public SessionAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.User.Identity?.IsAuthenticated == true)
        {
            var ticket = new AuthenticationTicket(Context.User, "Session");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        return Task.FromResult(AuthenticateResult.NoResult());
    }
}
