using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEraser.Application.DTOs;
using MicroEraser.Application.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace MicroEraser.Api.Controllers;

/// <summary>
/// Manages workspace invitations via email or shareable links.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Invites - Manage workspace invitations and shareable links")]
public class InviteController : ControllerBase
{
    private readonly InviteService _inviteService;

    public InviteController(InviteService inviteService)
    {
        _inviteService = inviteService;
    }

    [Authorize]
    [HttpGet("workspace/{workspaceId}")]
    public async Task<ActionResult<IEnumerable<InviteDto>>> GetWorkspaceInvites(Guid workspaceId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var invites = await _inviteService.GetWorkspaceInvitesAsync(workspaceId, userId.Value);
            return Ok(invites);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [Authorize]
    [HttpPost("workspace/{workspaceId}/email")]
    public async Task<ActionResult<InviteDto>> CreateEmailInvite(Guid workspaceId, [FromBody] CreateEmailInviteRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var invite = await _inviteService.CreateEmailInviteAsync(workspaceId, request, userId.Value);
            return Ok(invite);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [Authorize]
    [HttpPost("workspace/{workspaceId}/link")]
    public async Task<ActionResult<InviteDto>> CreateLinkInvite(Guid workspaceId, [FromBody] CreateLinkInviteRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var invite = await _inviteService.CreateLinkInviteAsync(workspaceId, request, userId.Value);
            return Ok(invite);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// Validate an invite token. Can be called without authentication.
    /// Used to check if an invite is valid before the user logs in or signs up.
    /// </summary>
    [HttpGet("validate/{token}")]
    public async Task<ActionResult<InviteValidationDto>> ValidateInvite(string token)
    {
        var validation = await _inviteService.ValidateInviteAsync(token);
        return Ok(validation);
    }

    /// <summary>
    /// Accept an invite to join a workspace.
    /// Requires authentication - user must be logged in.
    /// </summary>
    [Authorize]
    [HttpPost("accept")]
    public async Task<ActionResult<WorkspaceMemberDto>> AcceptInvite([FromBody] AcceptInviteRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var member = await _inviteService.AcceptInviteAsync(request.Token, userId.Value);
            return Ok(member);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteInvite(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            await _inviteService.DeleteInviteAsync(id, userId.Value);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value;
        
        return userIdClaim != null ? Guid.Parse(userIdClaim) : null;
    }
}
