namespace MicroEraser.Application.DTOs;

/// <summary>
/// DTO for receiving log events from the frontend client.
/// Enables end-to-end tracing of user actions from UI to backend.
/// </summary>
public record ClientLogRequest
{
    /// <summary>
    /// Type of event being logged (e.g., "node_created", "canvas_joined", "error")
    /// </summary>
    public string EventType { get; init; } = string.Empty;
    
    /// <summary>
    /// Log level: debug, info, warn, error
    /// </summary>
    public string Level { get; init; } = "info";
    
    /// <summary>
    /// Human-readable message describing the event
    /// </summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>
    /// ISO 8601 timestamp when the event occurred on the client
    /// </summary>
    public string Timestamp { get; init; } = string.Empty;
    
    /// <summary>
    /// Correlation ID for tracing across frontend-backend boundary
    /// </summary>
    public string? CorrelationId { get; init; }
    
    /// <summary>
    /// ID of the canvas being worked on (if applicable)
    /// </summary>
    public string? CanvasId { get; init; }
    
    /// <summary>
    /// ID of the workspace (if applicable)
    /// </summary>
    public string? WorkspaceId { get; init; }
    
    /// <summary>
    /// Additional context data as key-value pairs
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Batch of client logs for efficient transmission
/// </summary>
public record ClientLogBatchRequest
{
    public List<ClientLogRequest> Logs { get; init; } = new();
}

/// <summary>
/// Response indicating how many logs were processed
/// </summary>
public record ClientLogResponse
{
    public int Processed { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Standard event types for frontend logging
/// </summary>
public static class ClientEventTypes
{
    // Navigation events
    public const string PageView = "page_view";
    public const string NavigationStart = "navigation_start";
    public const string NavigationEnd = "navigation_end";
    
    // Authentication events
    public const string LoginAttempt = "login_attempt";
    public const string LoginSuccess = "login_success";
    public const string LoginFailure = "login_failure";
    public const string Logout = "logout";
    public const string TokenRefresh = "token_refresh";
    
    // Canvas operations
    public const string CanvasOpened = "canvas_opened";
    public const string CanvasClosed = "canvas_closed";
    public const string NodeCreated = "node_created";
    public const string NodeUpdated = "node_updated";
    public const string NodeDeleted = "node_deleted";
    public const string EdgeCreated = "edge_created";
    public const string EdgeDeleted = "edge_deleted";
    public const string CanvasSaved = "canvas_saved";
    
    // Collaboration events
    public const string CollaboratorJoined = "collaborator_joined";
    public const string CollaboratorLeft = "collaborator_left";
    public const string SyncStarted = "sync_started";
    public const string SyncCompleted = "sync_completed";
    public const string SyncError = "sync_error";
    public const string ConnectionLost = "connection_lost";
    public const string ConnectionRestored = "connection_restored";
    
    // Workspace events
    public const string WorkspaceCreated = "workspace_created";
    public const string WorkspaceDeleted = "workspace_deleted";
    public const string MemberInvited = "member_invited";
    public const string InviteAccepted = "invite_accepted";
    
    // Error events
    public const string Error = "error";
    public const string UnhandledError = "unhandled_error";
    public const string ApiError = "api_error";
}
