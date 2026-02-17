namespace MicroEraser.Application.DTOs;

public record CreateCanvasRequest(string Name, string? Description);

public record UpdateCanvasRequest(string Name, string? Description);

public record CanvasDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CanvasDetailDto(
    Guid Id,
    Guid WorkspaceId,
    string WorkspaceName,
    string Name,
    string? Description,
    bool HasSnapshot,
    long? LatestVersion,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Used to save Yjs document state from client.
/// State is base64 encoded Yjs binary update.
/// </summary>
public record SaveSnapshotRequest(string State, long Version);

public record SnapshotDto(
    Guid Id,
    Guid CanvasId,
    string State,
    long Version,
    DateTime CreatedAt
);
