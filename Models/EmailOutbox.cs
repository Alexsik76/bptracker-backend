using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BpTracker.Api.Models;

public enum EmailStatus
{
    Pending,
    Sent,
    Failed,
    Dead
}

public class EmailOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string To { get; set; } = string.Empty;

    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public string? AttachmentsJson { get; set; }

    public EmailStatus Status { get; set; } = EmailStatus.Pending;

    public int Attempts { get; set; }
    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UserId { get; set; }

    [JsonIgnore]
    public User? User { get; set; }
}
