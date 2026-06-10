using System;
using System.Text.Json;

namespace BpTracker.Api.Models;

public class ReminderTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SchemaId { get; set; }
    public TreatmentSchema Schema { get; set; } = null!;
    public bool IsActive { get; set; }
    public int DurationMinutes { get; set; }
    public int MaxReminders { get; set; }
    public JsonDocument Periods { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
