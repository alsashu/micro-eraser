using MicroEraser.Application.DTOs;
using MicroEraser.Application.Interfaces;
using MicroEraser.Domain.Entities;
using System.Security.Cryptography;

namespace MicroEraser.Application.Services;

public class InviteService
{
    private readonly IInviteRepository _inviteRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IUserRepository _userRepository;

    public InviteService(
        IInviteRepository inviteRepository,
        IWorkspaceRepository workspaceRepository,
        IUserRepository userRepository)
    {
        _inviteRepository = inviteRepository;
        _workspaceRepository = workspaceRepository;
        _userRepository = userRepository;
    }

    public async Task<IEnumerable<InviteDto>> GetWorkspaceInvitesAsync(Guid workspaceId, Guid userId)
    {
        // Verify user is admin
        var member = await _workspaceRepository.GetMemberAsync(workspaceId, userId);
        if (member == null || member.Role != WorkspaceRole.Admin)
        {
            throw new UnauthorizedAccessException("Only admins can view invites");
        }

        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId);
        if (workspace == null)
        {
            throw new InvalidOperationException("Workspace not found");
        }

        var invites = await _inviteRepository.GetByWorkspaceIdAsync(workspaceId);

        return invites.Select(i => new InviteDto(
            i.Id,
            i.WorkspaceId,
            workspace.Name,
            i.Email,
            i.Token,
            i.Permission,
            i.ExpiresAt,
            i.IsUsed,
            i.MaxUses,
            i.UseCount,
            i.CreatedAt
        ));
    }

    public async Task<InviteDto> CreateEmailInviteAsync(Guid workspaceId, CreateEmailInviteRequest request, Guid userId)
    {
        // Verify user is admin
        var member = await _workspaceRepository.GetMemberAsync(workspaceId, userId);
        if (member == null || member.Role != WorkspaceRole.Admin)
        {
            throw new UnauthorizedAccessException("Only admins can create invites");
        }

        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId);
        if (workspace == null)
        {
            throw new InvalidOperationException("Workspace not found");
        }

        // Check if invite already exists for this email
        var existingInvite = await _inviteRepository.GetByEmailAndWorkspaceAsync(request.Email.ToLowerInvariant(), workspaceId);
        if (existingInvite != null && existingInvite.IsValid)
        {
            throw new InvalidOperationException("An active invite already exists for this email");
        }

        // Check if user is already a member
        var invitedUser = await _userRepository.GetByEmailAsync(request.Email.ToLowerInvariant());
        if (invitedUser != null && await _workspaceRepository.IsMemberAsync(workspaceId, invitedUser.Id))
        {
            throw new InvalidOperationException("User is already a member of this workspace");
        }

        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Email = request.Email.ToLowerInvariant(),
            Token = GenerateInviteToken(),
            Permission = request.Permission,
            ExpiresAt = DateTime.UtcNow.AddHours(request.ExpiryHours),
            MaxUses = 1, // Email invites are single-use
            CreatedAt = DateTime.UtcNow
        };

        await _inviteRepository.CreateAsync(invite);

        return new InviteDto(
            invite.Id,
            invite.WorkspaceId,
            workspace.Name,
            invite.Email,
            invite.Token,
            invite.Permission,
            invite.ExpiresAt,
            invite.IsUsed,
            invite.MaxUses,
            invite.UseCount,
            invite.CreatedAt
        );
    }

    public async Task<InviteDto> CreateLinkInviteAsync(Guid workspaceId, CreateLinkInviteRequest request, Guid userId)
    {
        // Verify user is admin
        var member = await _workspaceRepository.GetMemberAsync(workspaceId, userId);
        if (member == null || member.Role != WorkspaceRole.Admin)
        {
            throw new UnauthorizedAccessException("Only admins can create invites");
        }

        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId);
        if (workspace == null)
        {
            throw new InvalidOperationException("Workspace not found");
        }

        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Email = null, // Link invites don't have a specific email
            Token = GenerateInviteToken(),
            Permission = request.Permission,
            ExpiresAt = DateTime.UtcNow.AddHours(request.ExpiryHours),
            MaxUses = request.MaxUses,
            CreatedAt = DateTime.UtcNow
        };

        await _inviteRepository.CreateAsync(invite);

        return new InviteDto(
            invite.Id,
            invite.WorkspaceId,
            workspace.Name,
            invite.Email,
            invite.Token,
            invite.Permission,
            invite.ExpiresAt,
            invite.IsUsed,
            invite.MaxUses,
            invite.UseCount,
            invite.CreatedAt
        );
    }

    public async Task<InviteValidationDto> ValidateInviteAsync(string token)
    {
        var invite = await _inviteRepository.GetByTokenAsync(token);

        if (invite == null)
        {
            return new InviteValidationDto(false, null, null, "Invite not found");
        }

        if (invite.IsExpired)
        {
            return new InviteValidationDto(false, null, null, "Invite has expired");
        }

        if (!invite.IsValid)
        {
            return new InviteValidationDto(false, null, null, "Invite is no longer valid");
        }

        var workspace = await _workspaceRepository.GetByIdAsync(invite.WorkspaceId);

        return new InviteValidationDto(
            true,
            workspace?.Name,
            invite.Permission,
            null
        );
    }

    public async Task<WorkspaceMemberDto> AcceptInviteAsync(string token, Guid userId)
    {
        var invite = await _inviteRepository.GetByTokenAsync(token);

        if (invite == null)
        {
            throw new InvalidOperationException("Invite not found");
        }

        if (!invite.IsValid)
        {
            throw new InvalidOperationException("Invite is no longer valid or has expired");
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Check if email-specific invite matches user's email
        if (invite.Email != null && invite.Email.ToLowerInvariant() != user.Email.ToLowerInvariant())
        {
            throw new UnauthorizedAccessException("This invite is for a different email address");
        }

        // Check if already a member
        if (await _workspaceRepository.IsMemberAsync(invite.WorkspaceId, userId))
        {
            throw new InvalidOperationException("You are already a member of this workspace");
        }

        // Add user to workspace
        var role = invite.Permission == InvitePermission.Edit ? WorkspaceRole.Editor : WorkspaceRole.Viewer;
        var member = new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = invite.WorkspaceId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };

        await _workspaceRepository.AddMemberAsync(member);

        // Update invite usage
        invite.UseCount++;
        invite.UsedAt = DateTime.UtcNow;
        invite.UsedByUserId = userId;
        
        if (invite.MaxUses.HasValue && invite.UseCount >= invite.MaxUses.Value)
        {
            invite.IsUsed = true;
        }

        await _inviteRepository.UpdateAsync(invite);

        return new WorkspaceMemberDto(
            user.Id,
            user.Name,
            user.Email,
            user.AvatarUrl,
            role,
            member.JoinedAt
        );
    }

    public async Task DeleteInviteAsync(Guid inviteId, Guid userId)
    {
        var invite = await _inviteRepository.GetByIdAsync(inviteId);
        
        if (invite == null)
        {
            throw new InvalidOperationException("Invite not found");
        }

        // Verify user is admin
        var member = await _workspaceRepository.GetMemberAsync(invite.WorkspaceId, userId);
        if (member == null || member.Role != WorkspaceRole.Admin)
        {
            throw new UnauthorizedAccessException("Only admins can delete invites");
        }

        await _inviteRepository.DeleteAsync(inviteId);
    }

    private static string GenerateInviteToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
