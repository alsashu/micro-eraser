using MicroEraser.Domain.Entities;

namespace MicroEraser.Application.Interfaces;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id);
    Task<Workspace?> GetByIdWithMembersAsync(Guid id);
    Task<IEnumerable<Workspace>> GetByUserIdAsync(Guid userId);
    Task<Workspace> CreateAsync(Workspace workspace);
    Task<Workspace> UpdateAsync(Workspace workspace);
    Task DeleteAsync(Guid id);
    Task<bool> IsMemberAsync(Guid workspaceId, Guid userId);
    Task<WorkspaceMember?> GetMemberAsync(Guid workspaceId, Guid userId);
    Task<WorkspaceMember> AddMemberAsync(WorkspaceMember member);
    Task RemoveMemberAsync(Guid workspaceId, Guid userId);
}
