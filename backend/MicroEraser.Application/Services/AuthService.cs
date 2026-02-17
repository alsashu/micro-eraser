using MicroEraser.Application.DTOs;
using MicroEraser.Application.Interfaces;
using MicroEraser.Domain.Entities;
using BCrypt.Net;

namespace MicroEraser.Application.Services;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly ICanvasRepository _canvasRepository;

    public AuthService(
        IUserRepository userRepository,
        ITokenService tokenService,
        IWorkspaceRepository workspaceRepository,
        ICanvasRepository canvasRepository)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _workspaceRepository = workspaceRepository;
        _canvasRepository = canvasRepository;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Check if user already exists
        if (await _userRepository.ExistsAsync(request.Email))
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        // Create new user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.ToLowerInvariant(),
            Name = request.Name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(user);

        // Create default workspace for new user
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = $"{request.Name}'s Workspace",
            Description = "Your default workspace",
            OwnerId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _workspaceRepository.CreateAsync(workspace);

        // Add user as admin member
        await _workspaceRepository.AddMemberAsync(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = user.Id,
            Role = WorkspaceRole.Admin,
            JoinedAt = DateTime.UtcNow
        });

        // Create demo canvas
        var canvas = new Canvas
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Name = "Getting Started",
            Description = "Welcome to MicroEraser! Start collaborating here.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _canvasRepository.CreateAsync(canvas);

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken(user.Id);

        return new AuthResponse(
            user.Id,
            user.Email,
            user.Name,
            accessToken,
            refreshToken.Token,
            refreshToken.ExpiresAt
        );
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email.ToLowerInvariant());
        
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken(user.Id);

        return new AuthResponse(
            user.Id,
            user.Email,
            user.Name,
            accessToken,
            refreshToken.Token,
            refreshToken.ExpiresAt
        );
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var refreshToken = await _tokenService.GetRefreshTokenAsync(request.RefreshToken);
        
        if (refreshToken == null || !refreshToken.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        var user = await _userRepository.GetByIdAsync(refreshToken.UserId);
        
        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        // Rotate refresh token for security
        var newRefreshToken = await _tokenService.RotateRefreshTokenAsync(refreshToken);
        var accessToken = _tokenService.GenerateAccessToken(user);

        return new AuthResponse(
            user.Id,
            user.Email,
            user.Name,
            accessToken,
            newRefreshToken.Token,
            newRefreshToken.ExpiresAt
        );
    }

    public async Task<UserDto?> GetUserAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        
        if (user == null) return null;

        return new UserDto(
            user.Id,
            user.Email,
            user.Name,
            user.AvatarUrl,
            user.CreatedAt
        );
    }

    public async Task RevokeTokenAsync(string token)
    {
        var refreshToken = await _tokenService.GetRefreshTokenAsync(token);
        
        if (refreshToken != null && refreshToken.IsActive)
        {
            await _tokenService.RevokeRefreshTokenAsync(refreshToken, "Revoked by user");
        }
    }
}
