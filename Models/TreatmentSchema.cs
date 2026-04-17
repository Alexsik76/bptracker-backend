using System.Text.Json;

namespace BpTracker.Api.Models;

public class TreatmentSchema
{
    public string Id { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public JsonDocument? ScheduleDocument { get; set; }
}
