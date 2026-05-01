using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BpTracker.Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BpTracker.Api.Tests.Measurements;

public class CrudTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CrudTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AddMeasurement_ReturnsCreated_AndAppearsInList()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        var createRes = await client.PostJsonAsync("/api/v1/measurements", new
        {
            sys = TestData.NormalSys,
            dia = TestData.NormalDia,
            pulse = TestData.NormalPulse
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();

        var list = await (await client.GetAsync("/api/v1/measurements"))
            .Content.ReadFromJsonAsync<JsonElement[]>();

        list.Should().Contain(m => m.GetProperty("id").GetString() == id);
    }

    // These test that CHECK constraints surface as 400, not 500.
    // Bug found while writing tests: the endpoint previously had no range guard,
    // so Postgres threw on SaveChanges. Fixed in MeasurementEndpoints.cs.
    [Theory]
    [InlineData(350, 80, 70)]   // sys too high (max 300)
    [InlineData(30, 80, 70)]    // sys too low  (min 40)
    public async Task AddMeasurement_OutOfRange_Returns400(int sys, int dia, int pulse)
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        var res = await client.PostJsonAsync("/api/v1/measurements", new { sys, dia, pulse });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteMeasurement_Removes_FromList()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        var createRes = await client.PostJsonAsync("/api/v1/measurements", new
        {
            sys = TestData.NormalSys,
            dia = TestData.NormalDia,
            pulse = TestData.NormalPulse
        });
        var created = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();

        (await client.DeleteAsync($"/api/v1/measurements/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await (await client.GetAsync("/api/v1/measurements"))
            .Content.ReadFromJsonAsync<JsonElement[]>();

        list.Should().NotContain(m => m.GetProperty("id").GetString() == id);
    }
}
