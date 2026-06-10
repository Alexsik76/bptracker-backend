namespace BpTracker.Api.DTOs;

public record PushSubscriptionKeysDto(string P256dh, string Auth);

public record PushSubscribeDto(string Endpoint, PushSubscriptionKeysDto Keys);

public record PushUnsubscribeDto(string Endpoint);
