using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BpTracker.Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BpTracker.Api.Tests.Measurements;

public class IsolationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public IsolationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMeasurements_DoesNotReturnOtherUserData()
    {
        var (_, tokenA) = await TestUser.CreateAsync(_factory);
        var (_, tokenB) = await TestUser.CreateAsync(_factory);

        var clientA = _factory.CreateClient().AuthAs(tokenA);
        var clientB = _factory.CreateClient().AuthAs(tokenB);

        for (var i = 0; i < 3; i++)
            await clientA.PostJsonAsync("/api/v1/measurements", new
            {
                sys = TestData.NormalSys, dia = TestData.NormalDia, pulse = TestData.NormalPulse
            });

        for (var i = 0; i < 2; i++)
            await clientB.PostJsonAsync("/api/v1/measurements", new
            {
                sys = TestData.NormalSys, dia = TestData.NormalDia, pulse = TestData.NormalPulse
            });

        var listA = await (await clientA.GetAsync("/api/v1/measurements"))
            .Content.ReadFromJsonAsync<JsonElement[]>();
        var listB = await (await clientB.GetAsync("/api/v1/measurements"))
            .Content.ReadFromJsonAsync<JsonElement[]>();

        listA!.Should().HaveCount(3);
        listB!.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteMeasurement_OfOtherUser_Returns404()
    {
        var (_, tokenA) = await TestUser.CreateAsync(_factory);
        var (_, tokenB) = await TestUser.CreateAsync(_factory);

        var clientA = _factory.CreateClient().AuthAs(tokenA);
        var clientB = _factory.CreateClient().AuthAs(tokenB);

        // User A creates a measurement
        var createRes = await clientA.PostJsonAsync("/api/v1/measurements", new
        {
            sys = TestData.NormalSys, dia = TestData.NormalDia, pulse = TestData.NormalPulse
        });
        var created = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var idOfA = created.GetProperty("id").GetString();

        // User B tries to delete user A's measurement — 404, not 403 (avoids resource existence leak)
        (await clientB.DeleteAsync($"/api/v1/measurements/{idOfA}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // User A's measurement is still intact
        var list = await (await clientA.GetAsync("/api/v1/measurements"))
            .Content.ReadFromJsonAsync<JsonElement[]>();
        list.Should().Contain(m => m.GetProperty("id").GetString() == idOfA);
    }

    [Fact]
    public async Task Settings_DoesNotLeakOtherUserData()
    {
        var (_, tokenA) = await TestUser.CreateAsync(_factory);
        var (_, tokenB) = await TestUser.CreateAsync(_factory);

        var clientA = _factory.CreateClient().AuthAs(tokenA);
        var clientB = _factory.CreateClient().AuthAs(tokenB);

        // User A sets a unique export email
        var emailA = $"export-a-{Guid.NewGuid():N}@example.com";
        await clientA.PatchJsonAsync("/api/v1/settings", new { exportEmail = emailA });

        // User B reads their own settings — should not contain user A's email
        var settingsB = await (await clientB.GetAsync("/api/v1/settings"))
            .Content.ReadFromJsonAsync<JsonElement>();
        settingsB.GetProperty("exportEmail").GetString().Should().NotBe(emailA);

        // User B patches their own settings
        var emailB = $"export-b-{Guid.NewGuid():N}@example.com";
        await clientB.PatchJsonAsync("/api/v1/settings", new { exportEmail = emailB });

        // User A's settings must be unchanged
        var settingsAfterA = await (await clientA.GetAsync("/api/v1/settings"))
            .Content.ReadFromJsonAsync<JsonElement>();
        settingsAfterA.GetProperty("exportEmail").GetString().Should().Be(emailA);
    }
}
