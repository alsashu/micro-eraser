namespace MicroEraser.Domain.Entities;

/// <summary>
/// Stores Yjs document state as binary snapshots.
/// Used for persistence and recovery of CRDT state.
/// 
/// CRDT Sync Flow:
/// 1. Client connects to canvas room via Socket.IO
/// 2. Server loads latest snapshot and sends as initial state
/// 3. Client merges snapshot with local Yjs document
/// 4. Updates are broadcast as Yjs update deltas
/// 5. Periodic snapshots are saved to database
/// </summary>
public class CanvasSnapshot
{
    public Guid Id { get; set; }
    public Guid CanvasId { get; set; }
    
    /// <summary>
    /// Binary Yjs document state encoded as base64.
    /// Contains the full CRDT state that can be loaded into a Y.Doc.
    /// </summary>
    public byte[] State { get; set; } = Array.Empty<byte>();
    
    /// <summary>
    /// Version number for ordering snapshots.
    /// </summary>
    public long Version { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Canvas Canvas { get; set; } = null!;
}
