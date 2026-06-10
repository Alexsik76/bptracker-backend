using BpTracker.Api.Data;
using BpTracker.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;
using Fido2NetLib;
using Microsoft.Extensions.Configuration;

namespace BpTracker.Api.Tests.Infrastructure;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16").Build();

    public FakeEmailSender EmailSender { get; } = new();
    public FakeGeminiService GeminiService { get; } = new();
    public FakePhotoApiService PhotoApiService { get; } = new();
    public FakeFido2 Fido2 { get; } = new();
    public FakeWebPushClient WebPushClient { get; } = new();
    public HashSet<string>? AllowedEmails { get; set; } = null;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VAPID_PUBLIC_KEY"] = "test-vapid-public-key",
                ["VAPID_PRIVATE_KEY"] = "test-vapid-private-key",
                ["VAPID_SUBJECT"] = "mailto:test@example.com"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace Fido2 with fake
            services.RemoveAll<IFido2>();
            services.AddSingleton<IFido2>(Fido2);

            // Replace WebPush client with fake
            services.RemoveAll<IWebPushClient>();
            services.AddSingleton<IWebPushClient>(WebPushClient);

            // Replace AuthSettings options dynamically
            services.RemoveAll<Microsoft.Extensions.Options.IOptions<BpTracker.Api.Models.AuthSettings>>();
            services.AddTransient<Microsoft.Extensions.Options.IOptions<BpTracker.Api.Models.AuthSettings>>(sp =>
            {
                var allowed = AllowedEmails ?? EmailSender.Captured.Select(e => e.To).ToHashSet();
                return Microsoft.Extensions.Options.Options.Create(new BpTracker.Api.Models.AuthSettings { AllowedEmails = allowed });
            });
            // The production connection string is baked into DbContextOptions via a closure
            // in Program.cs, so ConfigureAppConfiguration is too late. Replace the options directly.
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseNpgsql(_db.GetConnectionString()));

            // Replace email sender with in-memory fake
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            // Replace Gemini service with configurable fake
            services.RemoveAll<IGeminiService>();
            services.AddSingleton<IGeminiService>(GeminiService);

            // Replace Photo API service with configurable fake
            services.RemoveAll<IPhotoApiService>();
            services.AddSingleton<IPhotoApiService>(PhotoApiService);

            // Remove background outbox worker to prevent interference with status assertions
            var worker = services.FirstOrDefault(d => d.ImplementationType == typeof(EmailOutboxWorker));
            if (worker != null) services.Remove(worker);
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync() => await _db.StartAsync();

    public new async Task DisposeAsync() => await _db.DisposeAsync();
}
