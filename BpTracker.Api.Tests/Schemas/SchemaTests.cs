using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BpTracker.Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BpTracker.Api.Tests.Schemas;

public class SchemaTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SchemaTests(ApiFactory factory) => _factory = factory;

    // ── helpers ───────────────────────────────────────────────────────────────

    private static object ValidSchedule(string medicine = "Валсакор 80 мг") => new
    {
        Morning = new[]
        {
            new { Medicine = medicine, Amount = "1.0", Condition = "None" }
        }
    };

    private static object CreateBody(
        string doctor      = "Кардіолог Іваненко",
        bool setActive     = false,
        object? schedule   = null,
        string? prescribedOn = null) => new
    {
        doctor,
        prescribedOn,
        schedule = schedule ?? ValidSchedule(),
        setActive
    };

    private async Task<JsonElement> PostSchemaAsync(
        HttpClient client,
        string doctor    = "Кардіолог Іваненко",
        bool setActive   = false,
        object? schedule = null)
    {
        var res = await client.PostJsonAsync(
            "/api/v1/schemas",
            CreateBody(doctor: doctor, setActive: setActive, schedule: schedule));
        res.StatusCode.Should().Be(HttpStatusCode.Created,
            $"POST /schemas returned {(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement[]> GetAllAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/v1/schemas");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<JsonElement[]>())!;
    }

    // ── authorization ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GET",  "/api/v1/schemas")]
    [InlineData("POST", "/api/v1/schemas")]
    [InlineData("PUT",  "/api/v1/schemas/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/schemas/00000000-0000-0000-0000-000000000001/activate")]
    public async Task ProtectedEndpoints_WithoutSession_Return401(string method, string url)
    {
        var client  = _factory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(CreateBody());

        var res = await client.SendAsync(request);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetActive_WithoutSession_IsPublic()
    {
        var res = await _factory.CreateClient().GetAsync("/api/v1/schemas/active");
        res.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    // ── create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_Returns201_WithValidUuidAndSavedFields()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        var res = await client.PostJsonAsync("/api/v1/schemas", new
        {
            doctor       = "Кардіолог Іваненко",
            prescribedOn = "2026-06-01",
            schedule     = ValidSchedule("Форксіга 10 мг"),
            setActive    = false
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var schema = await res.Content.ReadFromJsonAsync<JsonElement>();
        Guid.TryParse(schema.GetProperty("id").GetString(), out _)
            .Should().BeTrue("id має бути валідним UUID");
        schema.GetProperty("doctor").GetString().Should().Be("Кардіолог Іваненко");
        schema.GetProperty("prescribedOn").GetString().Should().Be("2026-06-01");
        schema.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    // ── invariant: exactly one active ─────────────────────────────────────────

    [Fact]
    public async Task Create_TwoWithSetActive_OnlyLastIsActive()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        var first  = await PostSchemaAsync(client, "Лікар Перший",  setActive: true);
        var second = await PostSchemaAsync(client, "Лікар Другий",  setActive: true);

        var firstId  = first.GetProperty("id").GetString()!;
        var secondId = second.GetProperty("id").GetString()!;

        var list = await GetAllAsync(client);

        list.Count(s => s.GetProperty("isActive").GetBoolean())
            .Should().Be(1, "глобально має бути рівно одна активна схема");

        list.Single(s => s.GetProperty("id").GetString() == firstId)
            .GetProperty("isActive").GetBoolean()
            .Should().BeFalse("перша схема має бути деактивована після активації другої");

        list.Single(s => s.GetProperty("id").GetString() == secondId)
            .GetProperty("isActive").GetBoolean()
            .Should().BeTrue("остання схема з setActive:true має залишатись активною");
    }

    [Fact]
    public async Task Activate_SwitchesActiveFromSecondToFirst()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        var first  = await PostSchemaAsync(client, "Лікар А", setActive: true);
        var second = await PostSchemaAsync(client, "Лікар Б", setActive: true);

        var firstId  = first.GetProperty("id").GetString()!;
        var secondId = second.GetProperty("id").GetString()!;

        (await client.PostAsync($"/api/v1/schemas/{firstId}/activate", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await GetAllAsync(client);

        list.Count(s => s.GetProperty("isActive").GetBoolean())
            .Should().Be(1, "після activate глобально має залишатись рівно одна активна схема");

        list.Single(s => s.GetProperty("id").GetString() == firstId)
            .GetProperty("isActive").GetBoolean().Should().BeTrue("перша схема має стати активною");

        list.Single(s => s.GetProperty("id").GetString() == secondId)
            .GetProperty("isActive").GetBoolean().Should().BeFalse("друга схема має бути деактивована");
    }

    // ── public GET /active ────────────────────────────────────────────────────

    [Fact]
    public async Task GetActive_ReturnsCurrentActiveSchema_Publicly()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var authed = _factory.CreateClient().AuthAs(token);

        var created  = await PostSchemaAsync(authed, "Публічний лікар", setActive: true);
        var activeId = created.GetProperty("id").GetString()!;

        var res = await _factory.CreateClient().GetAsync("/api/v1/schemas/active");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        (await res.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString().Should().Be(activeId);
    }

    // ── update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Put_UpdatesFields_Returns200()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        var created = await PostSchemaAsync(client, "Оригінальний лікар");
        var id = created.GetProperty("id").GetString()!;

        var res = await client.PutJsonAsync($"/api/v1/schemas/{id}", new
        {
            doctor       = "Оновлений лікар",
            prescribedOn = (string?)null,
            schedule     = ValidSchedule()
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await res.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("doctor").GetString().Should().Be("Оновлений лікар");
        updated.GetProperty("id").GetString().Should().Be(id);
    }

    [Fact]
    public async Task Put_NonExistentId_Returns404()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        var res = await client.PutJsonAsync($"/api/v1/schemas/{Guid.NewGuid()}", new
        {
            doctor       = "X",
            prescribedOn = (string?)null,
            schedule     = ValidSchedule()
        });

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Activate_NonExistentId_Returns404()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        (await client.PostAsync($"/api/v1/schemas/{Guid.NewGuid()}/activate", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Validation_EmptyDoctor_Returns400()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        (await client.PostJsonAsync("/api/v1/schemas", CreateBody(doctor: "  ")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Validation_ScheduleWithNoEntries_Returns400()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        (await client.PostJsonAsync("/api/v1/schemas",
            CreateBody(schedule: new { Morning = Array.Empty<object>() })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Validation_UnknownPeriodKey_Returns400()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        (await client.PostJsonAsync("/api/v1/schemas", CreateBody(schedule: new
        {
            Night = new[] { new { Medicine = "Аспірин", Amount = "1.0", Condition = "None" } }
        }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("")]
    public async Task Validation_InvalidAmount_Returns400(string amount)
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        (await client.PostJsonAsync("/api/v1/schemas", CreateBody(schedule: new
        {
            Morning = new[] { new { Medicine = "Аспірин", Amount = amount, Condition = "None" } }
        }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Amount зберігається як рядок ──────────────────────────────────────────

    [Fact]
    public async Task Amount_SentAsJsonNumber_StoredAsString()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        // Amount = 1.5 — число, не рядок
        var schema = await PostSchemaAsync(client, schedule: new
        {
            Morning = new[] { new { Medicine = "Аспірин", Amount = 1.5, Condition = "None" } }
        });
        var id = schema.GetProperty("id").GetString()!;

        var list = await GetAllAsync(client);
        var stored = list.Single(s => s.GetProperty("id").GetString() == id);

        var amountEl = stored
            .GetProperty("scheduleDocument")
            .GetProperty("Morning")[0]
            .GetProperty("Amount");

        amountEl.ValueKind.Should().Be(JsonValueKind.String,
            "Amount має зберігатись як JSON-рядок, не число");
        amountEl.GetString().Should().Be("1.5");
    }

    // ── Unicode ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Unicode_CyrillicMedicine_StoredAndReadCorrectly()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        const string medicine = "Аторіс 20 мг";

        var schema = await PostSchemaAsync(client, schedule: new
        {
            Evening = new[] { new { Medicine = medicine, Amount = "1.0", Condition = "None" } }
        });
        var id = schema.GetProperty("id").GetString()!;

        var list = await GetAllAsync(client);
        var stored = list.Single(s => s.GetProperty("id").GetString() == id);

        stored
            .GetProperty("scheduleDocument")
            .GetProperty("Evening")[0]
            .GetProperty("Medicine")
            .GetString()
            .Should().Be(medicine);
    }
}
