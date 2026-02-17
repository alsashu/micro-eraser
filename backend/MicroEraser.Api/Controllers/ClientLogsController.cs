using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEraser.Application.DTOs;
using Serilog.Context;
using Swashbuckle.AspNetCore.Annotations;

namespace MicroEraser.Api.Controllers;

/// <summary>
/// Receives and processes logs from frontend clients.
/// Enables end-to-end observability by forwarding client events to Seq.
/// </summary>
[ApiController]
[Route("api/client-logs")]
[SwaggerTag("Client Logs - Receive and process frontend event logs")]
public class ClientLogsController : ControllerBase
{
    private readonly ILogger<ClientLogsController> _logger;

    public ClientLogsController(ILogger<ClientLogsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Submit a single log event from the frontend client.
    /// </summary>
    /// <param name="request">The log event to record</param>
    /// <returns>Confirmation of log processing</returns>
    [HttpPost]
    [AllowAnonymous] // Allow logging even for unauthenticated users
    [SwaggerOperation(
        Summary = "Submit a client log event",
        Description = "Receives a log event from the frontend and forwards it to the logging infrastructure"
    )]
    [SwaggerResponse(200, "Log processed successfully", typeof(ClientLogResponse))]
    [SwaggerResponse(400, "Invalid log format")]
    public IActionResult SubmitLog([FromBody] ClientLogRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            return BadRequest(new { message = "EventType is required" });
        }

        var userId = GetUserId() ?? "anonymous";
        var correlationId = request.CorrelationId 
            ?? HttpContext.Items["CorrelationId"]?.ToString() 
            ?? Guid.NewGuid().ToString("N");

        // Enrich log context with client-provided data
        using (LogContext.PushProperty("ClientCorrelationId", correlationId))
        using (LogContext.PushProperty("ClientUserId", userId))
        using (LogContext.PushProperty("ClientCanvasId", request.CanvasId ?? "none"))
        using (LogContext.PushProperty("ClientWorkspaceId", request.WorkspaceId ?? "none"))
        using (LogContext.PushProperty("ClientTimestamp", request.Timestamp))
        using (LogContext.PushProperty("EventType", request.EventType))
        using (LogContext.PushProperty("Source", "Frontend"))
        {
            // Add metadata as properties
            if (request.Metadata != null)
            {
                foreach (var (key, value) in request.Metadata)
                {
                    LogContext.PushProperty($"Meta_{key}", value);
                }
            }

            // Log at appropriate level
            var message = $"[CLIENT] {request.EventType}: {request.Message}";
            
            switch (request.Level.ToLowerInvariant())
            {
                case "debug":
                    _logger.LogDebug(message);
                    break;
                case "warn":
                case "warning":
                    _logger.LogWarning(message);
                    break;
                case "error":
                    _logger.LogError(message);
                    break;
                default:
                    _logger.LogInformation(message);
                    break;
            }
        }

        return Ok(new ClientLogResponse 
        { 
            Processed = 1, 
            CorrelationId = correlationId 
        });
    }

    /// <summary>
    /// Submit multiple log events in a single request for efficiency.
    /// </summary>
    /// <param name="request">Batch of log events</param>
    /// <returns>Count of processed logs</returns>
    [HttpPost("batch")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Submit multiple client log events",
        Description = "Receives a batch of log events from the frontend for efficient transmission"
    )]
    [SwaggerResponse(200, "Logs processed successfully", typeof(ClientLogResponse))]
    [SwaggerResponse(400, "Invalid log format")]
    public IActionResult SubmitLogBatch([FromBody] ClientLogBatchRequest request)
    {
        if (request.Logs == null || request.Logs.Count == 0)
        {
            return BadRequest(new { message = "No logs provided" });
        }

        var userId = GetUserId() ?? "anonymous";
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() 
            ?? Guid.NewGuid().ToString("N");
        var processed = 0;

        foreach (var log in request.Logs)
        {
            if (string.IsNullOrWhiteSpace(log.EventType))
                continue;

            var logCorrelationId = log.CorrelationId ?? correlationId;

            using (LogContext.PushProperty("ClientCorrelationId", logCorrelationId))
            using (LogContext.PushProperty("ClientUserId", userId))
            using (LogContext.PushProperty("ClientCanvasId", log.CanvasId ?? "none"))
            using (LogContext.PushProperty("ClientWorkspaceId", log.WorkspaceId ?? "none"))
            using (LogContext.PushProperty("ClientTimestamp", log.Timestamp))
            using (LogContext.PushProperty("EventType", log.EventType))
            using (LogContext.PushProperty("Source", "Frontend"))
            {
                var message = $"[CLIENT] {log.EventType}: {log.Message}";
                
                switch (log.Level.ToLowerInvariant())
                {
                    case "debug":
                        _logger.LogDebug(message);
                        break;
                    case "warn":
                    case "warning":
                        _logger.LogWarning(message);
                        break;
                    case "error":
                        _logger.LogError(message);
                        break;
                    default:
                        _logger.LogInformation(message);
                        break;
                }
            }

            processed++;
        }

        return Ok(new ClientLogResponse 
        { 
            Processed = processed, 
            CorrelationId = correlationId 
        });
    }

    private string? GetUserId()
    {
        return User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User?.FindFirst("sub")?.Value;
    }
}
