namespace BpTracker.Api.Models;

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "BP Tracker";
    // true = StartTLS (port 587), false = plain (port 25), port 465 always SslOnConnect
    public bool UseTls { get; set; } = true;
}
