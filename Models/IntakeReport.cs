using System;

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
    public ReminderTemplate Template { get; set; } = null!;
    public string Period { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public IntakeStatus Status { get; set; }
    public DateTimeOffset Time { get; set; }
}
