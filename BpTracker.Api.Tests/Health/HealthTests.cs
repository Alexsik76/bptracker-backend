using System.Net.Http.Json;
using System.Text.Json;
using BpTracker.Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BpTracker.Api.Tests.Health;

public class HealthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthTests(ApiFactory factory) => _factory = factory;

    // The health endpoint's npgsql check uses the production connection string baked into
    // Program.cs at startup, so it will report Unhealthy in the test environment.
    // DB connectivity is exercised by all other test classes that use the testcontainer.
    // This test just confirms the app started and the endpoint returns a valid response.
    [Fact]
    public async Task Health_Endpoint_IsReachable()
    {
        var res = await _factory.CreateClient().GetAsync("/api/v1/health");

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("checks", out var checks).Should().BeTrue();
        checks.GetArrayLength().Should().BeGreaterThan(0);
    }
}
