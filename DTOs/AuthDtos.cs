using Fido2NetLib;

namespace BpTracker.Api.DTOs;

public record MagicLinkRequestDto(string Email);

public record MagicLinkConsumeRequest(string Token);

public record PasskeyRegisterBeginDto(string Email);

public record PasskeyLoginBeginDto(string? Email); // Email optional with resident keys

public record NativeLoginBeginResponse(string ChallengeId, object Options);
public record NativeLoginCompleteRequest(string ChallengeId, AuthenticatorAssertionRawResponse Assertion);
public record NativeLoginResponse(string Token, Guid UserId, string Email, DateTimeOffset ExpiresAt);
