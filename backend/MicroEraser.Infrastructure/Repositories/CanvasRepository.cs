using Microsoft.EntityFrameworkCore;
using MicroEraser.Application.Interfaces;
using MicroEraser.Domain.Entities;
using MicroEraser.Infrastructure.Data;

namespace MicroEraser.Infrastructure.Repositories;

public class CanvasRepository : ICanvasRepository
{
    private readonly AppDbContext _context;

    public CanvasRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Canvas?> GetByIdAsync(Guid id)
    {
        return await _context.Canvases.FindAsync(id);
    }

    public async Task<Canvas?> GetByIdWithWorkspaceAsync(Guid id)
    {
        return await _context.Canvases
            .Include(c => c.Workspace)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<Canvas>> GetByWorkspaceIdAsync(Guid workspaceId)
    {
        return await _context.Canvases
            .Where(c => c.WorkspaceId == workspaceId)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();
    }

    public async Task<Canvas> CreateAsync(Canvas canvas)
    {
        _context.Canvases.Add(canvas);
        await _context.SaveChangesAsync();
        return canvas;
    }

    public async Task<Canvas> UpdateAsync(Canvas canvas)
    {
        canvas.UpdatedAt = DateTime.UtcNow;
        _context.Canvases.Update(canvas);
        await _context.SaveChangesAsync();
        return canvas;
    }

    public async Task DeleteAsync(Guid id)
    {
        var canvas = await _context.Canvases.FindAsync(id);
        if (canvas != null)
        {
            _context.Canvases.Remove(canvas);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Retrieves the latest Yjs snapshot for a canvas.
    /// The snapshot contains the full CRDT state that can be loaded into a Y.Doc.
    /// </summary>
    public async Task<CanvasSnapshot?> GetLatestSnapshotAsync(Guid canvasId)
    {
        return await _context.CanvasSnapshots
            .Where(s => s.CanvasId == canvasId)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Saves a Yjs document snapshot.
    /// Only keeps the last 10 snapshots to manage storage.
    /// </summary>
    public async Task<CanvasSnapshot> SaveSnapshotAsync(CanvasSnapshot snapshot)
    {
        _context.CanvasSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();

        // Cleanup old snapshots (keep last 10)
        var oldSnapshots = await _context.CanvasSnapshots
            .Where(s => s.CanvasId == snapshot.CanvasId)
            .OrderByDescending(s => s.Version)
            .Skip(10)
            .ToListAsync();

        if (oldSnapshots.Any())
        {
            _context.CanvasSnapshots.RemoveRange(oldSnapshots);
            await _context.SaveChangesAsync();
        }

        return snapshot;
    }

    public async Task<IEnumerable<CanvasSnapshot>> GetSnapshotsAfterVersionAsync(Guid canvasId, long version)
    {
        return await _context.CanvasSnapshots
            .Where(s => s.CanvasId == canvasId && s.Version > version)
            .OrderBy(s => s.Version)
            .ToListAsync();
    }
}
