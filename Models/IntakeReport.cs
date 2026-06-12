using System;
using System.Text.Json.Serialization;

namespace BpTracker.Api.Models;

public enum IntakeStatus
{
    Confirmed,
    Missed
}

public class IntakeReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }

    [JsonIgnore]
    public ReminderTemplate Template { get; set; } = null!;
    public string Period { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public IntakeStatus Status { get; set; }
    public DateTimeOffset Time { get; set; }

    public Guid UserId { get; set; }

    [JsonIgnore]
    public User User { get; set; } = null!;
}
