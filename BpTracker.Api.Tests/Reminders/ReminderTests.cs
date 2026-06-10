using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using BpTracker.Api.Data;
using BpTracker.Api.DTOs;
using BpTracker.Api.Models;
using BpTracker.Api.Services;
using BpTracker.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BpTracker.Api.Tests.Reminders;

public class ReminderTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ReminderTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.WebPushClient.Sent.Clear();
        _factory.WebPushClient.NextResponseStatusCode = HttpStatusCode.OK;
    }

    private static object ValidPeriods() => new
    {
        Morning = new
        {
            Time = "08:00",
            Meds = new[] { "Lozap 50 mg", "Aspirin" }
        },
        Evening = new
        {
            Time = "20:00",
            Meds = new[] { "Atoris 20 mg" }
        }
    };

    private static CreateTemplateDto CreateTemplateBody(Guid schemaId, bool isActive = true) => new(
        schemaId,
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(ValidPeriods())),
        15,
        5,
        isActive
    );

    private async Task<Guid> CreateSchemaAsync(HttpClient client, string doctor = "Cardiologist", bool setActive = true)
    {
        var schedule = new
        {
            Morning = new[]
            {
                new { Medicine = "Aspirin", Amount = "1.0", Condition = "None" }
            }
        };

        var res = await client.PostJsonAsync("/api/v1/schemas", new
        {
            doctor,
            prescribedOn = "2026-06-01",
            schedule,
            setActive
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var schema = await res.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(schema.GetProperty("id").GetString()!);
    }

    [Theory]
    [InlineData("POST", "/api/v1/reminders/template")]
    [InlineData("PATCH", "/api/v1/reminders/template/00000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/api/v1/reminders/template/active")]
    [InlineData("POST", "/api/v1/reminders/confirm")]
    [InlineData("GET", "/api/v1/reminders/reports")]
    public async Task ProtectedEndpoints_WithoutSession_Return401(string method, string url)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST" && url.EndsWith("/template"))
        {
            request.Content = JsonContent.Create(CreateTemplateBody(Guid.NewGuid()));
        }
        else if (method == "POST" && url.EndsWith("/confirm"))
        {
            request.Content = JsonContent.Create(new ConfirmIntakeDto("Morning"));
        }
        else if (method == "PATCH")
        {
            request.Content = JsonContent.Create(new UpdateTemplateDto(null, 20, null, null));
        }

        var res = await client.SendAsync(request);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Template_CreateAndGetActive_ReturnsCorrectFields()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        var schemaId = await CreateSchemaAsync(client);

        var createDto = CreateTemplateBody(schemaId, isActive: true);
        var res = await client.PostJsonAsync("/api/v1/reminders/template", createDto);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await res.Content.ReadFromJsonAsync<ReminderTemplate>();
        created.Should().NotBeNull();
        created!.SchemaId.Should().Be(schemaId);
        created.DurationMinutes.Should().Be(15);
        created.MaxReminders.Should().Be(5);
        created.IsActive.Should().BeTrue();

        var activeRes = await client.GetAsync("/api/v1/reminders/template/active");
        activeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var active = await activeRes.Content.ReadFromJsonAsync<ReminderTemplate>();
        active.Should().NotBeNull();
        active!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Template_Patch_UpdatesFields()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        var schemaId = await CreateSchemaAsync(client);
        var createDto = CreateTemplateBody(schemaId, isActive: true);
        var createRes = await client.PostJsonAsync("/api/v1/reminders/template", createDto);
        var created = (await createRes.Content.ReadFromJsonAsync<ReminderTemplate>())!;

        var updateDto = new UpdateTemplateDto(null, 30, 10, false);
        var patchRes = await client.PatchAsync($"/api/v1/reminders/template/{created.Id}", JsonContent.Create(updateDto));
        patchRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await patchRes.Content.ReadFromJsonAsync<ReminderTemplate>();
        updated.Should().NotBeNull();
        updated!.DurationMinutes.Should().Be(30);
        updated.MaxReminders.Should().Be(10);
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SchemaDeactivation_DeactivatesReminderTemplate()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        var schemaAId = await CreateSchemaAsync(client, "Doctor A", setActive: true);
        var schemaBId = await CreateSchemaAsync(client, "Doctor B", setActive: false);

        var templateA = CreateTemplateBody(schemaAId, isActive: true);
        var createRes = await client.PostJsonAsync("/api/v1/reminders/template", templateA);
        var created = (await createRes.Content.ReadFromJsonAsync<ReminderTemplate>())!;
        created.IsActive.Should().BeTrue();

        var actRes2 = await client.PostAsync($"/api/v1/schemas/{schemaBId}/activate", null);
        actRes2.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var templateFromDb = await db.ReminderTemplates.FindAsync(created.Id);
        templateFromDb.Should().NotBeNull();
        templateFromDb!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Confirm_CreatesReportAndIsIdempotent()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        var schemaId = await CreateSchemaAsync(client);
        var createDto = CreateTemplateBody(schemaId, isActive: true);
        await client.PostJsonAsync("/api/v1/reminders/template", createDto);

        var confirmDto = new ConfirmIntakeDto("Morning");
        var res1 = await client.PostJsonAsync("/api/v1/reminders/confirm", confirmDto);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);
        var report1 = await res1.Content.ReadFromJsonAsync<IntakeReport>();
        report1.Should().NotBeNull();
        report1!.Status.Should().Be(IntakeStatus.Confirmed);

        var res2 = await client.PostJsonAsync("/api/v1/reminders/confirm", confirmDto);
        res2.StatusCode.Should().Be(HttpStatusCode.OK);
        var report2 = await res2.Content.ReadFromJsonAsync<IntakeReport>();
        report2.Should().NotBeNull();
        report2!.Id.Should().Be(report1.Id);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.IntakeReports.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public void Worker_EvaluatePeriod_Tests()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kiev");
        var template = new ReminderTemplate
        {
            DurationMinutes = 15,
            MaxReminders = 5
        };

        var config = new PeriodConfig
        {
            Time = "08:00",
            Meds = new() { "Aspirin" }
        };

        var existing = new IntakeReport { Status = IntakeStatus.Confirmed };
        var now = new DateTimeOffset(2026, 6, 10, 8, 0, 0, tz.GetUtcOffset(new DateTime(2026, 6, 10, 8, 0, 0)));
        var decision = ReminderWorker.EvaluatePeriod(now, tz, template, "Morning", config, existing);
        decision.Action.Should().Be(ReminderActionType.None);

        var beforeNow = now.AddMinutes(-5);
        decision = ReminderWorker.EvaluatePeriod(beforeNow, tz, template, "Morning", config, null);
        decision.Action.Should().Be(ReminderActionType.None);

        decision = ReminderWorker.EvaluatePeriod(now, tz, template, "Morning", config, null);
        decision.Action.Should().Be(ReminderActionType.SendPush);
        decision.ReminderIndex.Should().Be(1);

        var tickLater = now.AddMinutes(1);
        decision = ReminderWorker.EvaluatePeriod(tickLater, tz, template, "Morning", config, null);
        decision.Action.Should().Be(ReminderActionType.None);

        var dueLater = now.AddMinutes(3);
        decision = ReminderWorker.EvaluatePeriod(dueLater, tz, template, "Morning", config, null);
        decision.Action.Should().Be(ReminderActionType.SendPush);
        decision.ReminderIndex.Should().Be(2);

        var windowPassed = now.AddMinutes(16);
        decision = ReminderWorker.EvaluatePeriod(windowPassed, tz, template, "Morning", config, null);
        decision.Action.Should().Be(ReminderActionType.RecordMissed);
        decision.MissedReportTime.Should().Be(now.AddMinutes(12));
    }
}
