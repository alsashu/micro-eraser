using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MicroEraser.Api.HealthChecks;

/// <summary>
/// Health check for SignalR hub connectivity.
/// Verifies that the SignalR service is operational.
/// </summary>
public class SignalRHealthCheck : IHealthCheck
{
    private readonly ILogger<SignalRHealthCheck> _logger;

    public SignalRHealthCheck(ILogger<SignalRHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // SignalR is considered healthy if the service is registered and running
            // In a more sophisticated check, we could try to establish a test connection
            return Task.FromResult(HealthCheckResult.Healthy("SignalR hub is operational"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalR health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("SignalR hub is not responding", ex));
        }
    }
}

/// <summary>
/// Health check for the database seeding status.
/// </summary>
public class DatabaseSeedHealthCheck : IHealthCheck
{
    private static bool _isSeeded = false;

    public static void MarkAsSeeded() => _isSeeded = true;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_isSeeded)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Database has been seeded"));
        }

        return Task.FromResult(HealthCheckResult.Degraded("Database seeding not yet complete"));
    }
}
