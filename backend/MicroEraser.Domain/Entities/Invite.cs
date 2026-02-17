namespace MicroEraser.Domain.Entities;

/// <summary>
/// Represents an invitation to join a workspace.
/// Can be either email-based or link-based with configurable permissions.
/// </summary>
public class Invite
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    
    /// <summary>
    /// Email address for direct invite, null for link-based invites.
    /// </summary>
    public string? Email { get; set; }
    
    /// <summary>
    /// Unique token for link-based invites.
    /// </summary>
    public string Token { get; set; } = string.Empty;
    
    public InvitePermission Permission { get; set; } = InvitePermission.View;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsUsed { get; set; } = false;
    public DateTime? UsedAt { get; set; }
    public Guid? UsedByUserId { get; set; }
    
    /// <summary>
    /// Maximum number of uses for link invites. Null means unlimited.
    /// </summary>
    public int? MaxUses { get; set; }
    public int UseCount { get; set; } = 0;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsValid => !IsExpired && (MaxUses == null || UseCount < MaxUses);

    // Navigation property
    public Workspace Workspace { get; set; } = null!;
}

public enum InvitePermission
{
    View = 0,
    Edit = 1
}
