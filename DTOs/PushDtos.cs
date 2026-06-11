namespace BpTracker.Api.DTOs;

public record PushSubscriptionKeysDto(string P256dh, string Auth);

public record PushSubscribeDto(string Endpoint, PushSubscriptionKeysDto Keys);

public record PushUnsubscribeDto(string Endpoint);

public class PushTestRequest
{
    public string? Urgency { get; set; }
    public int? Ttl { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Period { get; set; }
    public string? Date { get; set; }
    public string? TemplateId { get; set; }
    public string? Tag { get; set; }
    public bool? Renotify { get; set; }
}

