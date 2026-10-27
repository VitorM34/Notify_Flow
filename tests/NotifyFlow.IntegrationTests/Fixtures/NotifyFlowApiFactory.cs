using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace NotifyFlow.IntegrationTests.Fixtures;

/// <summary>
/// WebApplicationFactory apontando a API real (Program.cs sem alterações de lógica, só exposto
/// via "public partial class Program" para isto ser possível) para o Postgres/RabbitMQ reais
/// subidos pela fixture, em vez dos valores fixos de appsettings.json.
/// </summary>
public sealed class NotifyFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly string _rabbitMqHost;
    private readonly int _rabbitMqPort;

    public NotifyFlowApiFactory(string postgresConnectionString, string rabbitMqHost, int rabbitMqPort)
    {
        _postgresConnectionString = postgresConnectionString;
        _rabbitMqHost = rabbitMqHost;
        _rabbitMqPort = rabbitMqPort;
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgresConnectionString,
                ["RabbitMq:Host"] = _rabbitMqHost,
                ["RabbitMq:Port"] = _rabbitMqPort.ToString(),
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest",
                ["RabbitMq:ExchangeName"] = "notifyflow.events",
                ["RabbitMq:RoutingKey"] = "notifications",
                ["RabbitMq:QueueName"] = "notifyflow.notifications",
                ["RabbitMq:DeadLetterExchangeName"] = "notifyflow.events.dlx",
                ["RabbitMq:DeadLetterQueueName"] = "notifyflow.notifications.dlq",
                // Polling curto nos testes para não esperar o padrão de produção (5s) a cada caso.
                ["Outbox:IntervalSeconds"] = "1",
                ["Outbox:BatchSize"] = "20",
                ["Outbox:MaxAttempts"] = "5"
            });
        });
    }
}
