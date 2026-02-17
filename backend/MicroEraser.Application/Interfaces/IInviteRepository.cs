using MicroEraser.Domain.Entities;

namespace MicroEraser.Application.Interfaces;

public interface IInviteRepository
{
    Task<Invite?> GetByIdAsync(Guid id);
    Task<Invite?> GetByTokenAsync(string token);
    Task<Invite?> GetByEmailAndWorkspaceAsync(string email, Guid workspaceId);
    Task<IEnumerable<Invite>> GetByWorkspaceIdAsync(Guid workspaceId);
    Task<Invite> CreateAsync(Invite invite);
    Task<Invite> UpdateAsync(Invite invite);
    Task DeleteAsync(Guid id);
    Task DeleteExpiredAsync();
}
