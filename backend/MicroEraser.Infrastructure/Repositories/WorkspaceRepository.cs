using Microsoft.EntityFrameworkCore;
using MicroEraser.Application.Interfaces;
using MicroEraser.Domain.Entities;
using MicroEraser.Infrastructure.Data;

namespace MicroEraser.Infrastructure.Repositories;

public class WorkspaceRepository : IWorkspaceRepository
{
    private readonly AppDbContext _context;

    public WorkspaceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Workspace?> GetByIdAsync(Guid id)
    {
        return await _context.Workspaces
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<Workspace?> GetByIdWithMembersAsync(Guid id)
    {
        return await _context.Workspaces
            .Include(w => w.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<IEnumerable<Workspace>> GetByUserIdAsync(Guid userId)
    {
        return await _context.Workspaces
            .Include(w => w.Members)
            .Where(w => w.Members.Any(m => m.UserId == userId))
            .OrderByDescending(w => w.UpdatedAt)
            .ToListAsync();
    }

    public async Task<Workspace> CreateAsync(Workspace workspace)
    {
        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();
        return workspace;
    }

    public async Task<Workspace> UpdateAsync(Workspace workspace)
    {
        workspace.UpdatedAt = DateTime.UtcNow;
        _context.Workspaces.Update(workspace);
        await _context.SaveChangesAsync();
        return workspace;
    }

    public async Task DeleteAsync(Guid id)
    {
        var workspace = await _context.Workspaces.FindAsync(id);
        if (workspace != null)
        {
            _context.Workspaces.Remove(workspace);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> IsMemberAsync(Guid workspaceId, Guid userId)
    {
        return await _context.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);
    }

    public async Task<WorkspaceMember?> GetMemberAsync(Guid workspaceId, Guid userId)
    {
        return await _context.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);
    }

    public async Task<WorkspaceMember> AddMemberAsync(WorkspaceMember member)
    {
        _context.WorkspaceMembers.Add(member);
        await _context.SaveChangesAsync();
        return member;
    }

    public async Task RemoveMemberAsync(Guid workspaceId, Guid userId)
    {
        var member = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);
        
        if (member != null)
        {
            _context.WorkspaceMembers.Remove(member);
            await _context.SaveChangesAsync();
        }
    }
}
