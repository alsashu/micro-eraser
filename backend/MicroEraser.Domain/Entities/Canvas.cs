namespace MicroEraser.Domain.Entities;

/// <summary>
/// Represents a single diagram canvas within a workspace.
/// Each canvas has its own Yjs document for CRDT-based collaboration.
/// </summary>
public class Canvas
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Workspace Workspace { get; set; } = null!;
    public ICollection<CanvasSnapshot> Snapshots { get; set; } = new List<CanvasSnapshot>();
}
