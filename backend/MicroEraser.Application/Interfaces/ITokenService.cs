using MicroEraser.Domain.Entities;

namespace MicroEraser.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    RefreshToken GenerateRefreshToken(Guid userId);
    Task<RefreshToken?> GetRefreshTokenAsync(string token);
    Task RevokeRefreshTokenAsync(RefreshToken token, string reason);
    Task<RefreshToken> RotateRefreshTokenAsync(RefreshToken oldToken);
}
