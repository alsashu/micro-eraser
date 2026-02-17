using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEraser.Application.DTOs;
using MicroEraser.Application.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace MicroEraser.Api.Controllers;

/// <summary>
/// Manages diagram canvases within workspaces, including CRDT state persistence.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[SwaggerTag("Canvases - Create and manage diagram canvases with Yjs state")]
public class CanvasController : ControllerBase
{
    private readonly CanvasService _canvasService;

    public CanvasController(CanvasService canvasService)
    {
        _canvasService = canvasService;
    }

    [HttpGet("workspace/{workspaceId}")]
    public async Task<ActionResult<IEnumerable<CanvasDto>>> GetWorkspaceCanvases(Guid workspaceId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var canvases = await _canvasService.GetWorkspaceCanvasesAsync(workspaceId, userId.Value);
            return Ok(canvases);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CanvasDetailDto>> GetCanvas(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var canvas = await _canvasService.GetCanvasDetailAsync(id, userId.Value);
            if (canvas == null) return NotFound();
            return Ok(canvas);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpPost("workspace/{workspaceId}")]
    public async Task<ActionResult<CanvasDto>> CreateCanvas(Guid workspaceId, [FromBody] CreateCanvasRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var canvas = await _canvasService.CreateCanvasAsync(workspaceId, request, userId.Value);
            return CreatedAtAction(nameof(GetCanvas), new { id = canvas.Id }, canvas);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CanvasDto>> UpdateCanvas(Guid id, [FromBody] UpdateCanvasRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var canvas = await _canvasService.UpdateCanvasAsync(id, request, userId.Value);
            return Ok(canvas);
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
    public async Task<ActionResult> DeleteCanvas(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            await _canvasService.DeleteCanvasAsync(id, userId.Value);
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

    /// <summary>
    /// Get the latest Yjs snapshot for a canvas.
    /// Called when a client connects to load the current CRDT state.
    /// </summary>
    [HttpGet("{id}/snapshot")]
    public async Task<ActionResult<SnapshotDto>> GetSnapshot(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var snapshot = await _canvasService.GetLatestSnapshotAsync(id, userId.Value);
            if (snapshot == null) return NoContent();
            return Ok(snapshot);
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

    /// <summary>
    /// Save a Yjs document snapshot.
    /// Called periodically by clients to persist CRDT state.
    /// </summary>
    [HttpPost("{id}/snapshot")]
    public async Task<ActionResult<SnapshotDto>> SaveSnapshot(Guid id, [FromBody] SaveSnapshotRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var snapshot = await _canvasService.SaveSnapshotAsync(id, request, userId.Value);
            return Ok(snapshot);
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
