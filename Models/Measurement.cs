using System.ComponentModel.DataAnnotations;

namespace BpTracker.Api.Models;

public class Measurement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    [Range(40, 300)]
    public int Sys { get; set; }

    [Range(20, 200)]
    public int Dia { get; set; }

    [Range(30, 250)]
    public int Pulse { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
