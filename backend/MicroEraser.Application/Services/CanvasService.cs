using MicroEraser.Application.DTOs;
using MicroEraser.Application.Interfaces;
using MicroEraser.Domain.Entities;

namespace MicroEraser.Application.Services;

public class CanvasService
{
    private readonly ICanvasRepository _canvasRepository;
    private readonly IWorkspaceRepository _workspaceRepository;

    public CanvasService(
        ICanvasRepository canvasRepository,
        IWorkspaceRepository workspaceRepository)
    {
        _canvasRepository = canvasRepository;
        _workspaceRepository = workspaceRepository;
    }

    public async Task<IEnumerable<CanvasDto>> GetWorkspaceCanvasesAsync(Guid workspaceId, Guid userId)
    {
        // Verify user is a member
        if (!await _workspaceRepository.IsMemberAsync(workspaceId, userId))
        {
            throw new UnauthorizedAccessException("You are not a member of this workspace");
        }

        var canvases = await _canvasRepository.GetByWorkspaceIdAsync(workspaceId);
        
        return canvases.Select(c => new CanvasDto(
            c.Id,
            c.WorkspaceId,
            c.Name,
            c.Description,
            c.CreatedAt,
            c.UpdatedAt
        ));
    }

    public async Task<CanvasDetailDto?> GetCanvasDetailAsync(Guid canvasId, Guid userId)
    {
        var canvas = await _canvasRepository.GetByIdWithWorkspaceAsync(canvasId);
        
        if (canvas == null) return null;

        // Verify user is a member of the workspace
        if (!await _workspaceRepository.IsMemberAsync(canvas.WorkspaceId, userId))
        {
            throw new UnauthorizedAccessException("You are not a member of this workspace");
        }

        var latestSnapshot = await _canvasRepository.GetLatestSnapshotAsync(canvasId);

        return new CanvasDetailDto(
            canvas.Id,
            canvas.WorkspaceId,
            canvas.Workspace.Name,
            canvas.Name,
            canvas.Description,
            latestSnapshot != null,
            latestSnapshot?.Version,
            canvas.CreatedAt,
            canvas.UpdatedAt
        );
    }

    public async Task<CanvasDto> CreateCanvasAsync(Guid workspaceId, CreateCanvasRequest request, Guid userId)
    {
        // Verify user is a member with edit permission
        var member = await _workspaceRepository.GetMemberAsync(workspaceId, userId);
        if (member == null || member.Role == WorkspaceRole.Viewer)
        {
            throw new UnauthorizedAccessException("You don't have permission to create canvases in this workspace");
        }

        var canvas = new Canvas
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _canvasRepository.CreateAsync(canvas);

        return new CanvasDto(
            canvas.Id,
            canvas.WorkspaceId,
            canvas.Name,
            canvas.Description,
            canvas.CreatedAt,
            canvas.UpdatedAt
        );
    }

    public async Task<CanvasDto> UpdateCanvasAsync(Guid canvasId, UpdateCanvasRequest request, Guid userId)
    {
        var canvas = await _canvasRepository.GetByIdWithWorkspaceAsync(canvasId);
        
        if (canvas == null)
        {
            throw new InvalidOperationException("Canvas not found");
        }

        // Verify user has edit permission
        var member = await _workspaceRepository.GetMemberAsync(canvas.WorkspaceId, userId);
        if (member == null || member.Role == WorkspaceRole.Viewer)
        {
            throw new UnauthorizedAccessException("You don't have permission to update this canvas");
        }

        canvas.Name = request.Name;
        canvas.Description = request.Description;
        canvas.UpdatedAt = DateTime.UtcNow;

        await _canvasRepository.UpdateAsync(canvas);

        return new CanvasDto(
            canvas.Id,
            canvas.WorkspaceId,
            canvas.Name,
            canvas.Description,
            canvas.CreatedAt,
            canvas.UpdatedAt
        );
    }

    public async Task DeleteCanvasAsync(Guid canvasId, Guid userId)
    {
        var canvas = await _canvasRepository.GetByIdWithWorkspaceAsync(canvasId);
        
        if (canvas == null)
        {
            throw new InvalidOperationException("Canvas not found");
        }

        // Verify user has admin permission
        var member = await _workspaceRepository.GetMemberAsync(canvas.WorkspaceId, userId);
        if (member == null || member.Role != WorkspaceRole.Admin)
        {
            throw new UnauthorizedAccessException("Only admins can delete canvases");
        }

        await _canvasRepository.DeleteAsync(canvasId);
    }

    /// <summary>
    /// Get the latest Yjs document snapshot for a canvas.
    /// Used when a client connects to load the current state.
    /// </summary>
    public async Task<SnapshotDto?> GetLatestSnapshotAsync(Guid canvasId, Guid userId)
    {
        var canvas = await _canvasRepository.GetByIdWithWorkspaceAsync(canvasId);
        
        if (canvas == null)
        {
            throw new InvalidOperationException("Canvas not found");
        }

        // Verify user is a member
        if (!await _workspaceRepository.IsMemberAsync(canvas.WorkspaceId, userId))
        {
            throw new UnauthorizedAccessException("You are not a member of this workspace");
        }

        var snapshot = await _canvasRepository.GetLatestSnapshotAsync(canvasId);
        
        if (snapshot == null) return null;

        return new SnapshotDto(
            snapshot.Id,
            snapshot.CanvasId,
            Convert.ToBase64String(snapshot.State),
            snapshot.Version,
            snapshot.CreatedAt
        );
    }

    /// <summary>
    /// Save a Yjs document snapshot.
    /// Called periodically to persist CRDT state.
    /// </summary>
    public async Task<SnapshotDto> SaveSnapshotAsync(Guid canvasId, SaveSnapshotRequest request, Guid userId)
    {
        var canvas = await _canvasRepository.GetByIdWithWorkspaceAsync(canvasId);
        
        if (canvas == null)
        {
            throw new InvalidOperationException("Canvas not found");
        }

        // Verify user has edit permission
        var member = await _workspaceRepository.GetMemberAsync(canvas.WorkspaceId, userId);
        if (member == null || member.Role == WorkspaceRole.Viewer)
        {
            throw new UnauthorizedAccessException("You don't have permission to save to this canvas");
        }

        var snapshot = new CanvasSnapshot
        {
            Id = Guid.NewGuid(),
            CanvasId = canvasId,
            State = Convert.FromBase64String(request.State),
            Version = request.Version,
            CreatedAt = DateTime.UtcNow
        };

        await _canvasRepository.SaveSnapshotAsync(snapshot);

        // Update canvas timestamp
        canvas.UpdatedAt = DateTime.UtcNow;
        await _canvasRepository.UpdateAsync(canvas);

        return new SnapshotDto(
            snapshot.Id,
            snapshot.CanvasId,
            request.State,
            snapshot.Version,
            snapshot.CreatedAt
        );
    }
}
