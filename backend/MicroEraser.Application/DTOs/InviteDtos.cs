using MicroEraser.Domain.Entities;

namespace MicroEraser.Application.DTOs;

public record CreateEmailInviteRequest(string Email, InvitePermission Permission, int ExpiryHours = 168);

public record CreateLinkInviteRequest(InvitePermission Permission, int ExpiryHours = 168, int? MaxUses = null);

public record InviteDto(
    Guid Id,
    Guid WorkspaceId,
    string WorkspaceName,
    string? Email,
    string Token,
    InvitePermission Permission,
    DateTime ExpiresAt,
    bool IsUsed,
    int? MaxUses,
    int UseCount,
    DateTime CreatedAt
);

public record AcceptInviteRequest(string Token);

public record InviteValidationDto(
    bool IsValid,
    string? WorkspaceName,
    InvitePermission? Permission,
    string? ErrorMessage
);
