using BpTracker.Api.Models;

namespace BpTracker.Api.Services;

public interface IAuthService
{
    Task<User?> GetUserByEmailAsync(string email);
    Task<User> CreateUserAsync(string email);
    Task<UserCredential?> GetCredentialByIdAsync(byte[] credentialId);
    Task AddCredentialAsync(User user, UserCredential credential);
    Task UpdateCredentialSignCountAsync(byte[] credentialId, uint signCount);

    Task<string> CreateSessionAsync(Guid userId);
    Task<User?> GetUserBySessionTokenAsync(string token);
    Task InvalidateSessionAsync(string token);

    Task<string?> CreateMagicLinkAsync(string email);
    Task<string?> ConsumeMagicLinkAsync(string token);
    Task<bool> CanRequestMagicLinkAsync(string email);
}
