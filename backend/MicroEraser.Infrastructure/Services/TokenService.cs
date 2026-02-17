using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MicroEraser.Application.Interfaces;
using MicroEraser.Domain.Entities;
using MicroEraser.Infrastructure.Data;

namespace MicroEraser.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;

    public TokenService(IConfiguration configuration, AppDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    public string GenerateAccessToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.Name),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var expirationMinutes = int.Parse(jwtSettings["AccessTokenExpirationMinutes"] ?? "15");
        
        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshToken GenerateRefreshToken(Guid userId)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var expirationDays = int.Parse(jwtSettings["RefreshTokenExpirationDays"] ?? "7");

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = GenerateRandomToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);
        _context.SaveChanges();

        return refreshToken;
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
    {
        return await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == token);
    }

    public async Task RevokeRefreshTokenAsync(RefreshToken token, string reason)
    {
        token.RevokedAt = DateTime.UtcNow;
        token.ReasonRevoked = reason;
        _context.RefreshTokens.Update(token);
        await _context.SaveChangesAsync();
    }

    public async Task<RefreshToken> RotateRefreshTokenAsync(RefreshToken oldToken)
    {
        // Revoke the old token
        oldToken.RevokedAt = DateTime.UtcNow;
        oldToken.ReasonRevoked = "Replaced by new token";

        // Generate new token
        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = oldToken.UserId,
            Token = GenerateRandomToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        oldToken.ReplacedByToken = newRefreshToken.Token;

        _context.RefreshTokens.Update(oldToken);
        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync();

        return newRefreshToken;
    }

    private static string GenerateRandomToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
