using System.Text.Json;

namespace BpTracker.Api.Models;

public class TreatmentSchema
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? Doctor { get; set; }

    public DateOnly? PrescribedOn { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsActive { get; set; }

    public JsonDocument? ScheduleDocument { get; set; }
}
