using System.ComponentModel.DataAnnotations;

namespace BpTracker.Api.Models;

public class PhotoApiSettings
{
    public bool Enabled { get; set; } = false;

    public string? Url { get; set; }

    public string? Token { get; set; }

    public string DeviceModel { get; set; } = "Paramed Expert-X";
}
