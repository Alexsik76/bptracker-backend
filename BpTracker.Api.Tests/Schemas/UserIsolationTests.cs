using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using BpTracker.Api.Models;
using BpTracker.Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BpTracker.Api.Tests.Schemas;

public class UserIsolationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public UserIsolationTests(ApiFactory factory) => _factory = factory;

    private static object ValidSchedule(string medicine) => new
    {
        Morning = new[]
        {
            new { Medicine = medicine, Amount = "1.0", Condition = "None" }
        }
    };

    private static object CreateSchemaBody(string doctor, string medicine) => new
    {
        doctor,
        prescribedOn = "2026-06-01",
        schedule = ValidSchedule(medicine),
        setActive = true
    };

    private static object CreateTemplateBody(Guid schemaId) => new
    {
        schemaId,
        periods = new
        {
            Morning = new
            {
                Time = "08:00",
                Meds = new[] { "Aspirin" }
            }
        },
        durationMinutes = 15,
        maxReminders = 5,
        isActive = true
    };

    [Fact]
    public async Task TreatmentChain_IsStrictlyIsolatedPerUser()
    {
        // 1. Create User A and User B
        var (_, tokenA) = await TestUser.CreateAsync(_factory);
        var clientA = _factory.CreateClient().AuthAs(tokenA);

        var (_, tokenB) = await TestUser.CreateAsync(_factory);
        var clientB = _factory.CreateClient().AuthAs(tokenB);

        // 2. User A creates Schema A
        var resSchemaA = await clientA.PostJsonAsync("/api/v1/schemas", CreateSchemaBody("Doctor A", "Medicine A"));
        resSchemaA.StatusCode.Should().Be(HttpStatusCode.Created);
        var schemaA = await resSchemaA.Content.ReadFromJsonAsync<JsonElement>();
        var schemaAId = Guid.Parse(schemaA.GetProperty("id").GetString()!);

        // 3. User B creates Schema B
        var resSchemaB = await clientB.PostJsonAsync("/api/v1/schemas", CreateSchemaBody("Doctor B", "Medicine B"));
        resSchemaB.StatusCode.Should().Be(HttpStatusCode.Created);
        var schemaB = await resSchemaB.Content.ReadFromJsonAsync<JsonElement>();
        var schemaBId = Guid.Parse(schemaB.GetProperty("id").GetString()!);

        // 4. Assert: Get Active schema returns only own schema
        var activeResA = await clientA.GetAsync("/api/v1/schemas/active");
        activeResA.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeSchemaA = await activeResA.Content.ReadFromJsonAsync<JsonElement>();
        activeSchemaA.GetProperty("id").GetString().Should().Be(schemaAId.ToString());

        var activeResB = await clientB.GetAsync("/api/v1/schemas/active");
        activeResB.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeSchemaB = await activeResB.Content.ReadFromJsonAsync<JsonElement>();
        activeSchemaB.GetProperty("id").GetString().Should().Be(schemaBId.ToString());

        // 5. Assert: GetAll schemas returns only own schemas
        var allResA = await clientA.GetFromJsonAsync<JsonElement[]>("/api/v1/schemas");
        allResA.Should().NotBeNull();
        allResA!.Select(s => s.GetProperty("id").GetString()).Should().Contain(schemaAId.ToString());
        allResA!.Select(s => s.GetProperty("id").GetString()).Should().NotContain(schemaBId.ToString());

        var allResB = await clientB.GetFromJsonAsync<JsonElement[]>("/api/v1/schemas");
        allResB.Should().NotBeNull();
        allResB!.Select(s => s.GetProperty("id").GetString()).Should().Contain(schemaBId.ToString());
        allResB!.Select(s => s.GetProperty("id").GetString()).Should().NotContain(schemaAId.ToString());

        // 6. User A creates Template A
        var resTemplateA = await clientA.PostJsonAsync("/api/v1/reminders/template", CreateTemplateBody(schemaAId));
        resTemplateA.StatusCode.Should().Be(HttpStatusCode.Created);
        var templateA = await resTemplateA.Content.ReadFromJsonAsync<JsonElement>();
        var templateAId = Guid.Parse(templateA.GetProperty("id").GetString()!);

        // 7. User B creates Template B
        var resTemplateB = await clientB.PostJsonAsync("/api/v1/reminders/template", CreateTemplateBody(schemaBId));
        resTemplateB.StatusCode.Should().Be(HttpStatusCode.Created);
        var templateB = await resTemplateB.Content.ReadFromJsonAsync<JsonElement>();
        var templateBId = Guid.Parse(templateB.GetProperty("id").GetString()!);

        // 8. Assert: Get Active Template returns only own template
        var activeTempResA = await clientA.GetAsync("/api/v1/reminders/template/active");
        activeTempResA.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeTempA = await activeTempResA.Content.ReadFromJsonAsync<JsonElement>();
        activeTempA.GetProperty("id").GetString().Should().Be(templateAId.ToString());

        var activeTempResB = await clientB.GetAsync("/api/v1/reminders/template/active");
        activeTempResB.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeTempB = await activeTempResB.Content.ReadFromJsonAsync<JsonElement>();
        activeTempB.GetProperty("id").GetString().Should().Be(templateBId.ToString());

        // 9. Cross-user modification attempts must return 404 (NotFound)
        // User A tries to update User B's schema
        var updateSchemaRes = await clientA.PutJsonAsync($"/api/v1/schemas/{schemaBId}", CreateSchemaBody("Hacker", "Meds"));
        updateSchemaRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // User A tries to activate User B's schema
        var activateSchemaRes = await clientA.PostAsync($"/api/v1/schemas/{schemaBId}/activate", null);
        activateSchemaRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // User A tries to patch User B's template
        var patchTemplateRes = await clientA.PatchJsonAsync($"/api/v1/reminders/template/{templateBId}", new { durationMinutes = 99 });
        patchTemplateRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 10. Reports isolation check
        // User A confirms intake -> creates a report
        var confirmResA = await clientA.PostJsonAsync("/api/v1/reminders/confirm", new { period = "Morning" });
        confirmResA.StatusCode.Should().Be(HttpStatusCode.OK);

        // User B confirms intake -> creates a report
        var confirmResB = await clientB.PostJsonAsync("/api/v1/reminders/confirm", new { period = "Morning" });
        confirmResB.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert User A only gets report for A
        var reportsResA = await clientA.GetFromJsonAsync<JsonElement[]>("/api/v1/reminders/reports?days=1");
        reportsResA.Should().NotBeNull();
        reportsResA!.All(r => r.GetProperty("templateId").GetString() == templateAId.ToString()).Should().BeTrue();

        // Assert User B only gets report for B
        var reportsResB = await clientB.GetFromJsonAsync<JsonElement[]>("/api/v1/reminders/reports?days=1");
        reportsResB.Should().NotBeNull();
        reportsResB!.All(r => r.GetProperty("templateId").GetString() == templateBId.ToString()).Should().BeTrue();
    }
}
