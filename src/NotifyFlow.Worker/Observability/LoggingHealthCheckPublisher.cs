using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NotifyFlow.Worker.Observability;

public sealed class LoggingHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly ILogger<LoggingHealthCheckPublisher> _logger;

    public LoggingHealthCheckPublisher(ILogger<LoggingHealthCheckPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        if (report.Status == HealthStatus.Healthy)
        {
            _logger.LogInformation("Health check: {Status}", report.Status);
            return Task.CompletedTask;
        }

        foreach (var entry in report.Entries.Where(e => e.Value.Status != HealthStatus.Healthy))
        {
            _logger.LogWarning(
                entry.Value.Exception,
                "Health check unhealthy | Check: {Check} | Status: {Status} | Description: {Description}",
                entry.Key, entry.Value.Status, entry.Value.Description);
        }

        return Task.CompletedTask;
    }
}
