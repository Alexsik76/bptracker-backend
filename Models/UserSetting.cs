using System.ComponentModel.DataAnnotations;

namespace BpTracker.Api.Models;

public class UserSetting
{
    [Key]
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string? GeminiUrl { get; set; }
    public string? ExportEmail { get; set; }
    public string? SheetsTemplateUrl { get; set; }
}
