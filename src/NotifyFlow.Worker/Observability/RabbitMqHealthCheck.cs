using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NotifyFlow.Worker.Messaging;
using RabbitMQ.Client;

namespace NotifyFlow.Worker.Observability;

public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqOptions _options;

    public RabbitMqHealthCheck(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password
        };

        try
        {
            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy("RabbitMQ connection established.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to connect to RabbitMQ.", ex);
        }
    }
}
