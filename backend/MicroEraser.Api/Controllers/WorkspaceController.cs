using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEraser.Application.DTOs;
using MicroEraser.Application.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace MicroEraser.Api.Controllers;

/// <summary>
/// Manages workspaces for organizing canvases and collaborators.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[SwaggerTag("Workspaces - Create and manage collaborative workspaces")]
public class WorkspaceController : ControllerBase
{
    private readonly WorkspaceService _workspaceService;

    public WorkspaceController(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkspaceDto>>> GetWorkspaces()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var workspaces = await _workspaceService.GetUserWorkspacesAsync(userId.Value);
        return Ok(workspaces);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkspaceDetailDto>> GetWorkspace(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var workspace = await _workspaceService.GetWorkspaceDetailAsync(id, userId.Value);
            if (workspace == null) return NotFound();
            return Ok(workspace);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<WorkspaceDto>> CreateWorkspace([FromBody] CreateWorkspaceRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var workspace = await _workspaceService.CreateWorkspaceAsync(request, userId.Value);
            return CreatedAtAction(nameof(GetWorkspace), new { id = workspace.Id }, workspace);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<WorkspaceDto>> UpdateWorkspace(Guid id, [FromBody] UpdateWorkspaceRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var workspace = await _workspaceService.UpdateWorkspaceAsync(id, request, userId.Value);
            return Ok(workspace);
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

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteWorkspace(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            await _workspaceService.DeleteWorkspaceAsync(id, userId.Value);
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

    [HttpPost("{id}/members")]
    public async Task<ActionResult<WorkspaceMemberDto>> AddMember(Guid id, [FromBody] AddMemberRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var member = await _workspaceService.AddMemberAsync(id, request, userId.Value);
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

    [HttpDelete("{id}/members/{memberUserId}")]
    public async Task<ActionResult> RemoveMember(Guid id, Guid memberUserId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            await _workspaceService.RemoveMemberAsync(id, memberUserId, userId.Value);
            return NoContent();
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

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value;
        
        return userIdClaim != null ? Guid.Parse(userIdClaim) : null;
    }
}
