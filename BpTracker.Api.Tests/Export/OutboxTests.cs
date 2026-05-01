using System.Net;
using System.Net.Http.Json;
using BpTracker.Api.Data;
using BpTracker.Api.Models;
using BpTracker.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BpTracker.Api.Tests.Export;

public class OutboxTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public OutboxTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ExportCsv_QueuesEmailInOutbox()
    {
        var exportTo = $"export_{Guid.NewGuid():N}@example.com";
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        // Configure export destination
        await client.PatchJsonAsync("/api/v1/settings", new { exportEmail = exportTo });

        // Add a measurement so CSV is non-empty
        await client.PostJsonAsync("/api/v1/measurements", new
        {
            sys = TestData.NormalSys,
            dia = TestData.NormalDia,
            pulse = TestData.NormalPulse
        });

        var res = await client.PostAsync("/api/v1/export/csv", null);
        res.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Verify outbox entry is queued with attachment
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entry = await db.EmailOutbox.FirstOrDefaultAsync(e => e.To == exportTo);
        entry.Should().NotBeNull();
        entry!.Status.Should().Be(EmailStatus.Pending);
        entry.AttachmentsJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExportCsv_RateLimit_BlocksSecondCall()
    {
        var exportTo = $"export_rl_{Guid.NewGuid():N}@example.com";
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        await client.PatchJsonAsync("/api/v1/settings", new { exportEmail = exportTo });

        (await client.PostAsync("/api/v1/export/csv", null))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        (await client.PostAsync("/api/v1/export/csv", null))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
