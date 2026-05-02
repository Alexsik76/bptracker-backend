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

namespace BpTracker.Api.Tests.Infrastructure;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16").Build();

    public FakeEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // The production connection string is baked into DbContextOptions via a closure
            // in Program.cs, so ConfigureAppConfiguration is too late. Replace the options directly.
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseNpgsql(_db.GetConnectionString()));

            // Replace email sender with in-memory fake
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            // Replace Gemini service with deterministic fake
            services.RemoveAll<IGeminiService>();
            services.AddSingleton<IGeminiService, FakeGeminiService>();

            // Replace Photo API service with no-op fake
            services.RemoveAll<IPhotoApiService>();
            services.AddSingleton<IPhotoApiService, FakePhotoApiService>();

            // Remove background outbox worker to prevent interference with status assertions
            var worker = services.FirstOrDefault(d => d.ImplementationType == typeof(EmailOutboxWorker));
            if (worker != null) services.Remove(worker);
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync() => await _db.StartAsync();

    public new async Task DisposeAsync() => await _db.DisposeAsync();
}
