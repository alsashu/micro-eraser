namespace MicroEraser.Application.DTOs;

public record RegisterRequest(string Email, string Name, string Password);

public record LoginRequest(string Email, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record AuthResponse(
    Guid UserId,
    string Email,
    string Name,
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);

public record UserDto(
    Guid Id,
    string Email,
    string Name,
    string? AvatarUrl,
    DateTime CreatedAt
);
