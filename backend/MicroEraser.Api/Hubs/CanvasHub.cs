using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MicroEraser.Application.Interfaces;

namespace MicroEraser.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time canvas collaboration.
/// 
/// CRDT Sync Flow:
/// 1. Client connects and joins a canvas room via JoinCanvas
/// 2. Server sends the latest snapshot to initialize client's Y.Doc
/// 3. Client broadcasts Yjs updates via SyncUpdate
/// 4. Other clients receive updates and merge into their Y.Doc
/// 5. Awareness updates (cursors, selections) sent via AwarenessUpdate
/// 6. Periodic snapshots saved to database for persistence
/// 
/// Room naming: canvas:{canvasId}
/// </summary>
[Authorize]
public class CanvasHub : Hub
{
    private readonly ICanvasRepository _canvasRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly ILogger<CanvasHub> _logger;

    // Track active users per canvas for presence (keyed by UserId string to prevent duplicates)
    private static readonly Dictionary<string, Dictionary<string, UserPresence>> _canvasUsers = new();
    // Track which connections belong to which user in which room
    private static readonly Dictionary<string, (string roomName, string odUserId)> _connectionMap = new();
    private static readonly object _lock = new();

    public CanvasHub(
        ICanvasRepository canvasRepository,
        IWorkspaceRepository workspaceRepository,
        ILogger<CanvasHub> logger)
    {
        _canvasRepository = canvasRepository;
        _workspaceRepository = workspaceRepository;
        _logger = logger;
    }

    /// <summary>
    /// Join a canvas room for real-time collaboration.
    /// Validates user permission and sends current state.
    /// </summary>
    public async Task JoinCanvas(string canvasIdStr)
    {
        _logger.LogInformation("JoinCanvas called with canvasId: {CanvasId}, ConnectionId: {ConnectionId}", 
            canvasIdStr, Context.ConnectionId);
        
        if (!Guid.TryParse(canvasIdStr, out var canvasId))
        {
            _logger.LogWarning("Invalid canvas ID format: {CanvasId}", canvasIdStr);
            await Clients.Caller.SendAsync("Error", "Invalid canvas ID");
            return;
        }

        var userId = GetUserId();
        if (userId == null)
        {
            _logger.LogWarning("User not authenticated for canvas {CanvasId}", canvasId);
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var canvas = await _canvasRepository.GetByIdWithWorkspaceAsync(canvasId);
        if (canvas == null)
        {
            await Clients.Caller.SendAsync("Error", "Canvas not found");
            return;
        }

        // Verify user has access
        var member = await _workspaceRepository.GetMemberAsync(canvas.WorkspaceId, userId.Value);
        if (member == null)
        {
            await Clients.Caller.SendAsync("Error", "Access denied");
            return;
        }

        var roomName = GetRoomName(canvasId);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

        // Get user info for presence (JWT uses "name" claim, not ClaimTypes.Name)
        var userName = Context.User?.FindFirst("name")?.Value 
            ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value 
            ?? "Anonymous";

        // Track user presence (by UserId to prevent duplicate avatars)
        var presence = new UserPresence
        {
            UserId = userId.Value.ToString(),
            UserName = userName,
            ConnectionId = Context.ConnectionId,
            CanEdit = member.Role != Domain.Entities.WorkspaceRole.Viewer,
            JoinedAt = DateTime.UtcNow.ToString("o")
        };

        bool isNewUser = false;
        var userIdStr = userId.Value.ToString();
        lock (_lock)
        {
            if (!_canvasUsers.ContainsKey(roomName))
            {
                _canvasUsers[roomName] = new Dictionary<string, UserPresence>();
            }
            
            // Check if this is a new user or reconnection
            isNewUser = !_canvasUsers[roomName].ContainsKey(userIdStr);
            
            // Update presence (replaces old connection if user was already present)
            _canvasUsers[roomName][userIdStr] = presence;
            
            // Track this connection for cleanup on disconnect
            _connectionMap[Context.ConnectionId] = (roomName, userIdStr);
        }

        // Send current snapshot to joining client
        var snapshot = await _canvasRepository.GetLatestSnapshotAsync(canvasId);
        if (snapshot != null)
        {
            await Clients.Caller.SendAsync("InitialState", Convert.ToBase64String(snapshot.State), snapshot.Version);
        }
        else
        {
            await Clients.Caller.SendAsync("InitialState", null, 0);
        }

        // Notify others of new user (only if they weren't already in the room)
        if (isNewUser)
        {
            await Clients.OthersInGroup(roomName).SendAsync("UserJoined", new
            {
                userId = userId.ToString(),
                userName = userName,
                canEdit = presence.CanEdit
            });
        }

        // Send current users to joining client
        List<UserPresence> currentUsers;
        lock (_lock)
        {
            currentUsers = _canvasUsers[roomName].Values.ToList();
        }
        await Clients.Caller.SendAsync("CurrentUsers", currentUsers);

        _logger.LogInformation("User {UserId} joined canvas {CanvasId}", userId, canvasId);
    }

    /// <summary>
    /// Leave a canvas room.
    /// </summary>
    public async Task LeaveCanvas(string canvasIdStr)
    {
        if (!Guid.TryParse(canvasIdStr, out var canvasId)) return;
        
        var userId = GetUserId();
        if (userId == null) return;
        
        var roomName = GetRoomName(canvasId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
        
        // Clean up connection mapping
        lock (_lock)
        {
            _connectionMap.Remove(Context.ConnectionId);
        }
        
        await RemoveUserPresence(roomName, userId.Value.ToString());
    }

    /// <summary>
    /// Broadcast a Yjs update to all other clients in the canvas.
    /// The update is a base64-encoded Yjs update binary.
    /// 
    /// Yjs updates are delta-based and designed to be merged.
    /// Each client's Y.Doc applies the update, resolving any conflicts
    /// automatically using CRDT semantics.
    /// </summary>
    public async Task SyncUpdate(string canvasIdStr, string update)
    {
        _logger.LogInformation("SyncUpdate received from ConnectionId: {ConnectionId}, CanvasId: {CanvasId}, UpdateLength: {Length}", 
            Context.ConnectionId, canvasIdStr, update?.Length ?? 0);
            
        if (!Guid.TryParse(canvasIdStr, out var canvasId))
        {
            _logger.LogWarning("Invalid canvas ID for SyncUpdate: {CanvasId}", canvasIdStr);
            return;
        }
        
        var userId = GetUserId();
        if (userId == null)
        {
            _logger.LogWarning("Unauthenticated SyncUpdate attempt");
            return;
        }

        var roomName = GetRoomName(canvasId);
        
        // Broadcast to all other clients in the room
        _logger.LogInformation("Broadcasting SyncUpdate to room {RoomName} from user {UserId}", roomName, userId);
        await Clients.OthersInGroup(roomName).SendAsync("SyncUpdate", update, userId.ToString());
    }

    /// <summary>
    /// Broadcast awareness update (cursor position, selection, etc).
    /// Awareness is ephemeral and not persisted.
    /// </summary>
    public async Task AwarenessUpdate(string canvasIdStr, string awarenessState)
    {
        if (!Guid.TryParse(canvasIdStr, out var canvasId)) return;
        
        var userId = GetUserId();
        if (userId == null) return;

        var roomName = GetRoomName(canvasId);
        
        await Clients.OthersInGroup(roomName).SendAsync("AwarenessUpdate", awarenessState, userId.ToString());
    }

    /// <summary>
    /// Save a snapshot of the current Yjs document state.
    /// Called periodically by clients to persist state.
    /// </summary>
    public async Task SaveSnapshot(string canvasIdStr, string state, long version)
    {
        if (!Guid.TryParse(canvasIdStr, out var canvasId)) return;
        
        var userId = GetUserId();
        if (userId == null) return;

        var canvas = await _canvasRepository.GetByIdWithWorkspaceAsync(canvasId);
        if (canvas == null) return;

        var member = await _workspaceRepository.GetMemberAsync(canvas.WorkspaceId, userId.Value);
        if (member == null || member.Role == Domain.Entities.WorkspaceRole.Viewer) return;

        var snapshot = new Domain.Entities.CanvasSnapshot
        {
            Id = Guid.NewGuid(),
            CanvasId = canvasId,
            State = Convert.FromBase64String(state),
            Version = version,
            CreatedAt = DateTime.UtcNow
        };

        await _canvasRepository.SaveSnapshotAsync(snapshot);

        _logger.LogInformation("Snapshot saved for canvas {CanvasId} at version {Version}", canvasId, version);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        _logger.LogInformation("Client connected - ConnectionId: {ConnectionId}, UserId: {UserId}", 
            Context.ConnectionId, userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected - ConnectionId: {ConnectionId}", Context.ConnectionId);
        
        // Find and remove user from the room they were in
        (string roomName, string odUserId)? mapping = null;
        
        lock (_lock)
        {
            if (_connectionMap.TryGetValue(Context.ConnectionId, out var map))
            {
                mapping = map;
                _connectionMap.Remove(Context.ConnectionId);
            }
        }

        if (mapping.HasValue)
        {
            await RemoveUserPresence(mapping.Value.roomName, mapping.Value.odUserId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task RemoveUserPresence(string roomName, string odUserId)
    {
        UserPresence? removedUser = null;
        bool shouldNotifyOthers = false;

        lock (_lock)
        {
            if (_canvasUsers.TryGetValue(roomName, out var roomUsers) && 
                roomUsers.TryGetValue(odUserId, out var user))
            {
                // Only remove if this is the current connection for this user
                // (prevents removing user if they reconnected with a new connection)
                if (user.ConnectionId == Context.ConnectionId)
                {
                    removedUser = user;
                    roomUsers.Remove(odUserId);
                    shouldNotifyOthers = true;

                    if (roomUsers.Count == 0)
                    {
                        _canvasUsers.Remove(roomName);
                    }
                }
            }
        }

        if (shouldNotifyOthers && removedUser != null)
        {
            await Clients.Group(roomName).SendAsync("UserLeft", new
            {
                userId = removedUser.UserId,
                userName = removedUser.UserName
            });

            _logger.LogInformation("User {UserId} ({UserName}) left room {RoomName}", 
                removedUser.UserId, removedUser.UserName, roomName);
        }
    }

    private static string GetRoomName(Guid canvasId) => $"canvas:{canvasId}";

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        return userIdClaim != null ? Guid.Parse(userIdClaim) : null;
    }
}

public class UserPresence
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;
    
    [JsonPropertyName("connectionId")]
    public string ConnectionId { get; set; } = string.Empty;
    
    [JsonPropertyName("canEdit")]
    public bool CanEdit { get; set; }
    
    [JsonPropertyName("joinedAt")]
    public string JoinedAt { get; set; } = string.Empty;
}
