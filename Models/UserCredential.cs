using System.ComponentModel.DataAnnotations;

namespace BpTracker.Api.Models;

public class UserCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [Required]
    public byte[] CredentialId { get; set; } = Array.Empty<byte>();

    [Required]
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();

    public uint SignCount { get; set; }

    public string? DeviceName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}
