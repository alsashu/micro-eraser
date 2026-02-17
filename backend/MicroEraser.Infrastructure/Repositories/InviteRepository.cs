using Microsoft.EntityFrameworkCore;
using MicroEraser.Application.Interfaces;
using MicroEraser.Domain.Entities;
using MicroEraser.Infrastructure.Data;

namespace MicroEraser.Infrastructure.Repositories;

public class InviteRepository : IInviteRepository
{
    private readonly AppDbContext _context;

    public InviteRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Invite?> GetByIdAsync(Guid id)
    {
        return await _context.Invites.FindAsync(id);
    }

    public async Task<Invite?> GetByTokenAsync(string token)
    {
        return await _context.Invites
            .Include(i => i.Workspace)
            .FirstOrDefaultAsync(i => i.Token == token);
    }

    public async Task<Invite?> GetByEmailAndWorkspaceAsync(string email, Guid workspaceId)
    {
        return await _context.Invites
            .FirstOrDefaultAsync(i => i.Email == email.ToLowerInvariant() && i.WorkspaceId == workspaceId && !i.IsUsed);
    }

    public async Task<IEnumerable<Invite>> GetByWorkspaceIdAsync(Guid workspaceId)
    {
        return await _context.Invites
            .Where(i => i.WorkspaceId == workspaceId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<Invite> CreateAsync(Invite invite)
    {
        _context.Invites.Add(invite);
        await _context.SaveChangesAsync();
        return invite;
    }

    public async Task<Invite> UpdateAsync(Invite invite)
    {
        _context.Invites.Update(invite);
        await _context.SaveChangesAsync();
        return invite;
    }

    public async Task DeleteAsync(Guid id)
    {
        var invite = await _context.Invites.FindAsync(id);
        if (invite != null)
        {
            _context.Invites.Remove(invite);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteExpiredAsync()
    {
        var expiredInvites = await _context.Invites
            .Where(i => i.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        if (expiredInvites.Any())
        {
            _context.Invites.RemoveRange(expiredInvites);
            await _context.SaveChangesAsync();
        }
    }
}
