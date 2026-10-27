using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotifyFlow.Worker.Persistence;

namespace NotifyFlow.Worker.Observability;

public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly NotifyFlowWorkerDbContext _dbContext;

    public PostgresHealthCheck(NotifyFlowWorkerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy("Postgres connection established.")
            : HealthCheckResult.Unhealthy("Cannot connect to Postgres.");
    }
}
