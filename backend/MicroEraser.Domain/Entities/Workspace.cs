namespace MicroEraser.Domain.Entities;

/// <summary>
/// Represents a collaborative workspace that contains multiple canvases.
/// Workspaces are owned by a user and can have multiple members with different roles.
/// </summary>
public class Workspace
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User Owner { get; set; } = null!;
    public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
    public ICollection<Canvas> Canvases { get; set; } = new List<Canvas>();
    public ICollection<Invite> Invites { get; set; } = new List<Invite>();
}
