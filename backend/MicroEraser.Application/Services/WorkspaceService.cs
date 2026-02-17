using MicroEraser.Application.DTOs;
using MicroEraser.Application.Interfaces;
using MicroEraser.Domain.Entities;

namespace MicroEraser.Application.Services;

public class WorkspaceService
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICanvasRepository _canvasRepository;

    public WorkspaceService(
        IWorkspaceRepository workspaceRepository,
        IUserRepository userRepository,
        ICanvasRepository canvasRepository)
    {
        _workspaceRepository = workspaceRepository;
        _userRepository = userRepository;
        _canvasRepository = canvasRepository;
    }

    public async Task<IEnumerable<WorkspaceDto>> GetUserWorkspacesAsync(Guid userId)
    {
        var workspaces = await _workspaceRepository.GetByUserIdAsync(userId);
        var result = new List<WorkspaceDto>();

        foreach (var ws in workspaces)
        {
            var owner = await _userRepository.GetByIdAsync(ws.OwnerId);
            var canvases = await _canvasRepository.GetByWorkspaceIdAsync(ws.Id);
            
            result.Add(new WorkspaceDto(
                ws.Id,
                ws.Name,
                ws.Description,
                ws.OwnerId,
                owner?.Name ?? "Unknown",
                ws.Members.Count,
                canvases.Count(),
                ws.CreatedAt,
                ws.UpdatedAt
            ));
        }

        return result;
    }

    public async Task<WorkspaceDetailDto?> GetWorkspaceDetailAsync(Guid workspaceId, Guid userId)
    {
        var workspace = await _workspaceRepository.GetByIdWithMembersAsync(workspaceId);
        
        if (workspace == null) return null;

        // Check if user is a member
        if (!await _workspaceRepository.IsMemberAsync(workspaceId, userId))
        {
            throw new UnauthorizedAccessException("You are not a member of this workspace");
        }

        var owner = await _userRepository.GetByIdAsync(workspace.OwnerId);
        var canvases = await _canvasRepository.GetByWorkspaceIdAsync(workspaceId);

        var members = new List<WorkspaceMemberDto>();
        foreach (var m in workspace.Members)
        {
            var user = await _userRepository.GetByIdAsync(m.UserId);
            if (user != null)
            {
                members.Add(new WorkspaceMemberDto(
                    user.Id,
                    user.Name,
                    user.Email,
                    user.AvatarUrl,
                    m.Role,
                    m.JoinedAt
                ));
            }
        }

        var canvasDtos = canvases.Select(c => new CanvasDto(
            c.Id,
            c.WorkspaceId,
            c.Name,
            c.Description,
            c.CreatedAt,
            c.UpdatedAt
        ));

        return new WorkspaceDetailDto(
            workspace.Id,
            workspace.Name,
            workspace.Description,
            workspace.OwnerId,
            owner?.Name ?? "Unknown",
            members,
            canvasDtos,
            workspace.CreatedAt,
            workspace.UpdatedAt
        );
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(CreateWorkspaceRequest request, Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _workspaceRepository.CreateAsync(workspace);

        // Add creator as admin
        await _workspaceRepository.AddMemberAsync(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = userId,
            Role = WorkspaceRole.Admin,
            JoinedAt = DateTime.UtcNow
        });

        return new WorkspaceDto(
            workspace.Id,
            workspace.Name,
            workspace.Description,
            workspace.OwnerId,
            user.Name,
            1,
            0,
            workspace.CreatedAt,
            workspace.UpdatedAt
        );
    }

    public async Task<WorkspaceDto> UpdateWorkspaceAsync(Guid workspaceId, UpdateWorkspaceRequest request, Guid userId)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId);
        
        if (workspace == null)
        {
            throw new InvalidOperationException("Workspace not found");
        }

        // Check if user is admin or owner
        var member = await _workspaceRepository.GetMemberAsync(workspaceId, userId);
        if (member == null || (member.Role != WorkspaceRole.Admin && workspace.OwnerId != userId))
        {
            throw new UnauthorizedAccessException("You don't have permission to update this workspace");
        }

        workspace.Name = request.Name;
        workspace.Description = request.Description;
        workspace.UpdatedAt = DateTime.UtcNow;

        await _workspaceRepository.UpdateAsync(workspace);

        var owner = await _userRepository.GetByIdAsync(workspace.OwnerId);
        var canvases = await _canvasRepository.GetByWorkspaceIdAsync(workspaceId);

        return new WorkspaceDto(
            workspace.Id,
            workspace.Name,
            workspace.Description,
            workspace.OwnerId,
            owner?.Name ?? "Unknown",
            workspace.Members.Count,
            canvases.Count(),
            workspace.CreatedAt,
            workspace.UpdatedAt
        );
    }

    public async Task DeleteWorkspaceAsync(Guid workspaceId, Guid userId)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId);
        
        if (workspace == null)
        {
            throw new InvalidOperationException("Workspace not found");
        }

        if (workspace.OwnerId != userId)
        {
            throw new UnauthorizedAccessException("Only the workspace owner can delete it");
        }

        await _workspaceRepository.DeleteAsync(workspaceId);
    }

    public async Task<WorkspaceMemberDto> AddMemberAsync(Guid workspaceId, AddMemberRequest request, Guid requesterId)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId);
        
        if (workspace == null)
        {
            throw new InvalidOperationException("Workspace not found");
        }

        // Check if requester is admin
        var requesterMember = await _workspaceRepository.GetMemberAsync(workspaceId, requesterId);
        if (requesterMember == null || requesterMember.Role != WorkspaceRole.Admin)
        {
            throw new UnauthorizedAccessException("Only admins can add members");
        }

        var user = await _userRepository.GetByEmailAsync(request.Email.ToLowerInvariant());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Check if already a member
        if (await _workspaceRepository.IsMemberAsync(workspaceId, user.Id))
        {
            throw new InvalidOperationException("User is already a member");
        }

        var member = new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = user.Id,
            Role = request.Role,
            JoinedAt = DateTime.UtcNow
        };

        await _workspaceRepository.AddMemberAsync(member);

        return new WorkspaceMemberDto(
            user.Id,
            user.Name,
            user.Email,
            user.AvatarUrl,
            member.Role,
            member.JoinedAt
        );
    }

    public async Task RemoveMemberAsync(Guid workspaceId, Guid memberUserId, Guid requesterId)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId);
        
        if (workspace == null)
        {
            throw new InvalidOperationException("Workspace not found");
        }

        // Owner cannot be removed
        if (memberUserId == workspace.OwnerId)
        {
            throw new InvalidOperationException("Cannot remove the workspace owner");
        }

        // Check if requester is admin or removing themselves
        var requesterMember = await _workspaceRepository.GetMemberAsync(workspaceId, requesterId);
        if (requesterMember == null || (requesterMember.Role != WorkspaceRole.Admin && requesterId != memberUserId))
        {
            throw new UnauthorizedAccessException("You don't have permission to remove this member");
        }

        await _workspaceRepository.RemoveMemberAsync(workspaceId, memberUserId);
    }
}
