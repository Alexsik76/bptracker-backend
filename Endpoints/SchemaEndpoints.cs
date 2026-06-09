using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using BpTracker.Api.DTOs;
using BpTracker.Api.Services;

namespace BpTracker.Api.Endpoints;

public static class SchemaEndpoints
{
    // Maps any casing of a period name to canonical PascalCase written to jsonb.
    // OrdinalIgnoreCase: morning/MORNING/Morning → "Morning"
    private static readonly Dictionary<string, string> CanonicalPeriod =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "Morning", "Morning" },
            { "Day",     "Day"     },
            { "Evening", "Evening" }
        };

    private static readonly JsonSerializerOptions ScheduleSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic)
    };

    public static void MapSchemaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/schemas");

        // Public — фронт залежить від цього ендпоінту, не чіпаємо
        group.MapGet("/active", async (ISchemaService service) =>
        {
            var schema = await service.GetActiveAsync();
            return schema is not null ? Results.Ok(schema) : Results.NotFound();
        });

        var auth = group.MapGroup("").RequireAuthorization();

        auth.MapGet("/", async (ISchemaService service) =>
            Results.Ok(await service.GetAllAsync()));

        auth.MapPost("/", async (CreateSchemaRequest req, ISchemaService service) =>
        {
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(req.Doctor))
                errors["doctor"] = ["Doctor is required"];

            if (!TryNormalizeSchedule(req.Schedule, out var scheduleDoc, out var schedErrors))
                foreach (var kv in schedErrors!)
                    errors[kv.Key] = kv.Value;

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var schema = await service.CreateAsync(
                req.Doctor.Trim(), req.PrescribedOn, scheduleDoc!, req.SetActive);

            return Results.Created($"/api/v1/schemas/{schema.Id}", schema);
        });

        auth.MapPut("/{id:guid}", async (Guid id, UpdateSchemaRequest req, ISchemaService service) =>
        {
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(req.Doctor))
                errors["doctor"] = ["Doctor is required"];

            if (!TryNormalizeSchedule(req.Schedule, out var scheduleDoc, out var schedErrors))
                foreach (var kv in schedErrors!)
                    errors[kv.Key] = kv.Value;

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var schema = await service.UpdateAsync(id, req.Doctor.Trim(), req.PrescribedOn, scheduleDoc!);
            return schema is not null ? Results.Ok(schema) : Results.NotFound();
        });

        auth.MapPost("/{id:guid}/activate", async (Guid id, ISchemaService service) =>
        {
            var activated = await service.ActivateAsync(id);
            return activated ? Results.NoContent() : Results.NotFound();
        });
    }

    private sealed record ScheduleEntry(string Medicine, string Amount, string Condition);

    private static bool TryNormalizeSchedule(
        JsonElement el,
        out JsonDocument? doc,
        out Dictionary<string, string[]>? errors)
    {
        doc = null;
        var err = new Dictionary<string, string[]>();

        if (el.ValueKind != JsonValueKind.Object)
        {
            errors = new() { ["schedule"] = ["Must be a JSON object"] };
            return false;
        }

        // First pass: reject unknown period keys before processing any entries
        foreach (var prop in el.EnumerateObject())
            if (!CanonicalPeriod.ContainsKey(prop.Name))
                err[$"schedule.{prop.Name}"] =
                    [$"Unknown period '{prop.Name}'; allowed: Morning, Day, Evening"];

        if (err.Count > 0) { errors = err; return false; }

        var normalized = new Dictionary<string, List<ScheduleEntry>>(StringComparer.Ordinal);
        int totalEntries = 0;

        // Second pass: build entries. TryGetProperty is case-sensitive, so iterate properties instead.
        foreach (var periodProp in el.EnumerateObject())
        {
            var canonical = CanonicalPeriod[periodProp.Name];
            var arr = periodProp.Value;

            if (arr.ValueKind != JsonValueKind.Array)
            {
                err[$"schedule.{canonical}"] = ["Must be an array"];
                continue;
            }

            var entries = new List<ScheduleEntry>();
            int i = 0;

            foreach (var item in arr.EnumerateArray())
            {
                string? medicine = null, amountRaw = null, condition = null;

                foreach (var field in item.EnumerateObject())
                {
                    if (string.Equals(field.Name, "Medicine", StringComparison.OrdinalIgnoreCase))
                        medicine = field.Value.GetString()?.Trim();
                    else if (string.Equals(field.Name, "Amount", StringComparison.OrdinalIgnoreCase))
                        amountRaw = field.Value.ValueKind == JsonValueKind.Number
                            ? field.Value.ToString()
                            : field.Value.GetString()?.Trim();
                    else if (string.Equals(field.Name, "Condition", StringComparison.OrdinalIgnoreCase))
                        condition = field.Value.GetString()?.Trim();
                }

                if (string.IsNullOrEmpty(medicine))
                    err[$"schedule.{canonical}[{i}].Medicine"] = ["Medicine is required"];

                if (string.IsNullOrEmpty(amountRaw) ||
                    !decimal.TryParse(amountRaw, NumberStyles.Number,
                        CultureInfo.InvariantCulture, out var amt) ||
                    amt <= 0)
                    err[$"schedule.{canonical}[{i}].Amount"] = ["Amount must be a positive number"];

                entries.Add(new ScheduleEntry(
                    medicine ?? "",
                    amountRaw ?? "",
                    string.IsNullOrEmpty(condition) ? "None" : condition));

                totalEntries++;
                i++;
            }

            if (entries.Count > 0)
                normalized[canonical] = entries;
        }

        if (totalEntries == 0)
            err["schedule"] = ["At least one entry required in Morning, Day, or Evening"];

        if (err.Count > 0) { errors = err; return false; }

        doc = JsonSerializer.SerializeToDocument(normalized, ScheduleSerializerOptions);
        errors = null;
        return true;
    }
}
