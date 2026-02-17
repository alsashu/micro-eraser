using MicroEraser.Domain.Entities;

namespace MicroEraser.Application.Interfaces;

public interface ICanvasRepository
{
    Task<Canvas?> GetByIdAsync(Guid id);
    Task<Canvas?> GetByIdWithWorkspaceAsync(Guid id);
    Task<IEnumerable<Canvas>> GetByWorkspaceIdAsync(Guid workspaceId);
    Task<Canvas> CreateAsync(Canvas canvas);
    Task<Canvas> UpdateAsync(Canvas canvas);
    Task DeleteAsync(Guid id);
    
    // Snapshot operations for Yjs CRDT persistence
    Task<CanvasSnapshot?> GetLatestSnapshotAsync(Guid canvasId);
    Task<CanvasSnapshot> SaveSnapshotAsync(CanvasSnapshot snapshot);
    Task<IEnumerable<CanvasSnapshot>> GetSnapshotsAfterVersionAsync(Guid canvasId, long version);
}
