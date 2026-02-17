namespace MicroEraser.Domain.Entities;

/// <summary>
/// Junction table linking users to workspaces with role-based permissions.
/// </summary>
public class WorkspaceMember
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public WorkspaceRole Role { get; set; } = WorkspaceRole.Viewer;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Workspace Workspace { get; set; } = null!;
    public User User { get; set; } = null!;
}

/// <summary>
/// Defines the permission levels within a workspace.
/// </summary>
public enum WorkspaceRole
{
    Viewer = 0,   // Can view canvases but not edit
    Editor = 1,   // Can edit canvases
    Admin = 2     // Can manage workspace settings and members
}
