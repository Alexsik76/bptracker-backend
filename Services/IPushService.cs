using System;
using System.Threading.Tasks;
using BpTracker.Api.DTOs;

namespace BpTracker.Api.Services;

public interface IPushService
{
    Task SaveSubscriptionAsync(Guid userId, PushSubscribeDto subscription);
    Task RemoveSubscriptionAsync(Guid userId, string endpoint);
    Task<(int sent, int failed)> SendToUserAsync(
        Guid userId,
        string title,
        string body,
        string? period = null,
        string? date = null,
        string? templateId = null);
}
