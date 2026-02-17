using MicroEraser.Domain.Entities;

namespace MicroEraser.Application.DTOs;

public record CreateWorkspaceRequest(string Name, string? Description);

public record UpdateWorkspaceRequest(string Name, string? Description);

public record WorkspaceDto(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    string OwnerName,
    int MemberCount,
    int CanvasCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record WorkspaceDetailDto(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    string OwnerName,
    IEnumerable<WorkspaceMemberDto> Members,
    IEnumerable<CanvasDto> Canvases,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record WorkspaceMemberDto(
    Guid UserId,
    string Name,
    string Email,
    string? AvatarUrl,
    WorkspaceRole Role,
    DateTime JoinedAt
);

public record AddMemberRequest(string Email, WorkspaceRole Role);
