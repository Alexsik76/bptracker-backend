namespace BpTracker.Api.DTOs;

public record MagicLinkRequestDto(string Email);

public record PasskeyRegisterBeginDto(string Email);

public record PasskeyLoginBeginDto(string? Email); // Email optional with resident keys
