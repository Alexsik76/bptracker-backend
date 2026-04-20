using System.ComponentModel.DataAnnotations;

namespace BpTracker.Api.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public string Email { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserCredential> Credentials { get; set; } = new List<UserCredential>();
    public UserSetting? Settings { get; set; }
    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
    public ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
}
