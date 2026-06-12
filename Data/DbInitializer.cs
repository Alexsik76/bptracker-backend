using System.Text.Json;
using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var devUserGuid = new Guid("e6915d20-30fd-4154-b43a-85c04dbac190");
        var devUser = await db.Users.FindAsync(devUserGuid);
        if (devUser == null)
        {
            devUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "dev@local");
            if (devUser == null)
            {
                devUser = new User
                {
                    Id = devUserGuid,
                    Email = "dev@local",
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.Users.Add(devUser);
                await db.SaveChangesAsync();
            }
            else
            {
                devUserGuid = devUser.Id;
            }
        }

        var schemaId = Guid.NewGuid();
        var hasSchemas = await db.TreatmentSchemas.AnyAsync(s => s.UserId == devUserGuid);
        if (!hasSchemas)
        {
            var scheduleObj = new
            {
                Morning = new[]
                {
                    new { Medicine = "Lozap 50 mg", Amount = "1.0", Condition = "None" },
                    new { Medicine = "Aspirin", Amount = "1.0", Condition = "None" }
                },
                Day = Array.Empty<object>(),
                Evening = new[]
                {
                    new { Medicine = "Atoris 20 mg", Amount = "1.0", Condition = "None" }
                }
            };

            var schema = new TreatmentSchema
            {
                Id = schemaId,
                Doctor = "Dr. House",
                PrescribedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
                IsActive = true,
                ScheduleDocument = JsonDocument.Parse(JsonSerializer.Serialize(scheduleObj)),
                UserId = devUserGuid,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-5)
            };

            db.TreatmentSchemas.Add(schema);
            await db.SaveChangesAsync();
        }
        else
        {
            var activeSchema = await db.TreatmentSchemas.FirstOrDefaultAsync(s => s.UserId == devUserGuid && s.IsActive);
            if (activeSchema != null)
            {
                schemaId = activeSchema.Id;
            }
        }

        var hasTemplates = await db.ReminderTemplates.AnyAsync(t => t.UserId == devUserGuid);
        if (!hasTemplates && schemaId != Guid.Empty)
        {
            var periodsObj = new
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

            var template = new ReminderTemplate
            {
                Id = Guid.NewGuid(),
                SchemaId = schemaId,
                IsActive = true,
                DurationMinutes = 15,
                MaxReminders = 5,
                Periods = JsonDocument.Parse(JsonSerializer.Serialize(periodsObj)),
                UserId = devUserGuid,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-5)
            };

            db.ReminderTemplates.Add(template);
            await db.SaveChangesAsync();
        }

        var hasMeasurements = await db.Measurements.AnyAsync(m => m.UserId == devUserGuid);
        if (!hasMeasurements)
        {
            var now = DateTimeOffset.UtcNow;
            var measurements = new List<Measurement>
            {
                new() { Id = Guid.NewGuid(), UserId = devUserGuid, Sys = 120, Dia = 80, Pulse = 72, RecordedAt = now.AddDays(-3).AddHours(2) },
                new() { Id = Guid.NewGuid(), UserId = devUserGuid, Sys = 125, Dia = 82, Pulse = 75, RecordedAt = now.AddDays(-2).AddHours(1) },
                new() { Id = Guid.NewGuid(), UserId = devUserGuid, Sys = 118, Dia = 79, Pulse = 68, RecordedAt = now.AddDays(-1).AddHours(3) },
                new() { Id = Guid.NewGuid(), UserId = devUserGuid, Sys = 122, Dia = 81, Pulse = 70, RecordedAt = now.AddHours(-4) }
            };

            db.Measurements.AddRange(measurements);
            await db.SaveChangesAsync();
        }
    }
}
