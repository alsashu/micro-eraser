using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Serilog.Context;

namespace MicroEraser.Api.Middleware;

/// <summary>
/// Middleware for comprehensive request/response logging.
/// Captures request details, response status, timing, and user context.
/// Logs are enriched with user ID, correlation ID, and request metadata.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    // Paths to exclude from detailed logging (health checks, etc.)
    private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/health-ui",
        "/favicon.ico"
    };

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip logging for excluded paths
        if (ShouldSkipLogging(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";

        // Extract user info if authenticated
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? context.User?.FindFirst("sub")?.Value;
        var userName = context.User?.FindFirst("name")?.Value 
            ?? context.User?.FindFirst(ClaimTypes.Name)?.Value;

        // Push user context to all logs in this request
        using (LogContext.PushProperty("UserId", userId ?? "anonymous"))
        using (LogContext.PushProperty("UserName", userName ?? "anonymous"))
        using (LogContext.PushProperty("RequestId", requestId))
        using (LogContext.PushProperty("ClientIP", context.Connection.RemoteIpAddress?.ToString()))
        {
            // Log request start
            _logger.LogInformation(
                "HTTP {RequestMethod} {RequestPath} started | Query: {QueryString} | User: {UserId}",
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString.ToString(),
                userId ?? "anonymous"
            );

            try
            {
                await _next(context);
                stopwatch.Stop();

                // Log successful response
                var level = context.Response.StatusCode >= 400 
                    ? LogLevel.Warning 
                    : LogLevel.Information;

                _logger.Log(
                    level,
                    "HTTP {RequestMethod} {RequestPath} completed | Status: {StatusCode} | Duration: {Duration}ms",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds
                );
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                // Log exception
                _logger.LogError(
                    ex,
                    "HTTP {RequestMethod} {RequestPath} failed | Duration: {Duration}ms | Error: {ErrorMessage}",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds,
                    ex.Message
                );
                
                throw;
            }
        }
    }

    private static bool ShouldSkipLogging(PathString path)
    {
        return ExcludedPaths.Any(excluded => 
            path.StartsWithSegments(excluded, StringComparison.OrdinalIgnoreCase));
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
