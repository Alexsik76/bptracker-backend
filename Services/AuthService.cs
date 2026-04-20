using System.Security.Cryptography;
using System.Text;
using BpTracker.Api.Data;
using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private const int SessionDays = 30;
    private const int MagicLinkMinutes = 15;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User> CreateUserAsync(string email)
    {
        var user = new User { Email = email };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<UserCredential?> GetCredentialByIdAsync(byte[] credentialId)
    {
        return await _db.UserCredentials
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CredentialId == credentialId);
    }

    public async Task AddCredentialAsync(User user, UserCredential credential)
    {
        credential.UserId = user.Id;
        _db.UserCredentials.Add(credential);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateCredentialSignCountAsync(byte[] credentialId, uint signCount)
    {
        var cred = await _db.UserCredentials.FirstOrDefaultAsync(c => c.CredentialId == credentialId);
        if (cred != null)
        {
            cred.SignCount = signCount;
            cred.LastUsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<string> CreateSessionAsync(Guid userId)
    {
        var token = GenerateSecureToken();
        var hash = HashToken(token);
        
        var session = new UserSession
        {
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(SessionDays)
        };
        
        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync();
        
        return token;
    }

    public async Task<User?> GetUserBySessionTokenAsync(string token)
    {
        var hash = HashToken(token);
        var session = await _db.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.TokenHash == hash && s.ExpiresAt > DateTime.UtcNow);
            
        return session?.User;
    }

    public async Task InvalidateSessionAsync(string token)
    {
        var hash = HashToken(token);
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.TokenHash == hash);
        if (session != null)
        {
            _db.UserSessions.Remove(session);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<string> CreateMagicLinkAsync(string email)
    {
        var token = GenerateSecureToken();
        var hash = HashToken(token);
        
        var link = new MagicLink
        {
            Email = email,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(MagicLinkMinutes)
        };
        
        _db.MagicLinks.Add(link);
        await _db.SaveChangesAsync();
        
        return token;
    }

    public async Task<string?> ConsumeMagicLinkAsync(string token)
    {
        var hash = HashToken(token);
        var link = await _db.MagicLinks
            .FirstOrDefaultAsync(l => l.TokenHash == hash && l.ExpiresAt > DateTime.UtcNow);
            
        if (link == null) return null;
        
        var email = link.Email;
        _db.MagicLinks.Remove(link);
        await _db.SaveChangesAsync();
        
        return email;
    }

    private static string GenerateSecureToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
