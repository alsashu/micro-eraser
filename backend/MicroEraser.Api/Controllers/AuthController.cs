using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEraser.Application.DTOs;
using MicroEraser.Application.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace MicroEraser.Api.Controllers;

/// <summary>
/// Handles user authentication including registration, login, token refresh, and logout.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Authentication - User registration, login, and token management")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Register a new user account.
    /// </summary>
    /// <param name="request">Registration details including email, name, and password</param>
    /// <returns>Authentication response with tokens</returns>
    [HttpPost("register")]
    [SwaggerOperation(Summary = "Register a new user", Description = "Creates a new user account and returns authentication tokens")]
    [SwaggerResponse(200, "Registration successful", typeof(AuthResponse))]
    [SwaggerResponse(400, "Invalid registration data or email already exists")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Authenticate an existing user.
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>Authentication response with tokens</returns>
    [HttpPost("login")]
    [SwaggerOperation(Summary = "Login user", Description = "Authenticates user and returns JWT access and refresh tokens")]
    [SwaggerResponse(200, "Login successful", typeof(AuthResponse))]
    [SwaggerResponse(401, "Invalid credentials")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var response = await _authService.RefreshTokenAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _authService.GetUserAsync(userId.Value);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<ActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        await _authService.RevokeTokenAsync(request.RefreshToken);
        return NoContent();
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value;
        
        return userIdClaim != null ? Guid.Parse(userIdClaim) : null;
    }
}
