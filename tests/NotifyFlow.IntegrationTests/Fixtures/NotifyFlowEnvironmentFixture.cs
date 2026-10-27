using Microsoft.EntityFrameworkCore;
using NotifyFlow.Api.Persistence;
using NotifyFlow.Worker.Persistence;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace NotifyFlow.IntegrationTests.Fixtures;

/// <summary>
/// Sobe PostgreSQL e RabbitMQ reais (Testcontainers, mesmas imagens do
/// infrastructure/docker-compose.yml) uma única vez para toda a suíte de integração — ver
/// NotifyFlowEnvironmentCollection ([CollectionFixture]). Decisão de isolamento (E06-T03, Passo
/// 12): container compartilhado em vez de um por classe (mais rápido); isolamento entre casos
/// vem de cada teste usar identificadores únicos (EventId/Email via Guid), não de recriar a
/// infraestrutura a cada teste.
/// </summary>
public sealed class NotifyFlowEnvironmentFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("notifyflow")
        .WithUsername("notifyflow")
        .WithPassword("notifyflow")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private IConnection _rabbitConnection = null!;

    public string PostgresConnectionString { get; private set; } = string.Empty;

    public string RabbitMqHost { get; private set; } = string.Empty;

    public int RabbitMqPort { get; private set; }

    public NotifyFlowApiFactory ApiFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        PostgresConnectionString =
            $"Host={_postgres.Hostname};Port={_postgres.GetMappedPublicPort(5432)};Database=notifyflow;Username=notifyflow;Password=notifyflow";

        RabbitMqHost = _rabbitMq.Hostname;
        RabbitMqPort = _rabbitMq.GetMappedPublicPort(5672);

        // src/NotifyFlow.Api/Program.cs lê a config do RabbitMQ/Postgres e conecta de verdade
        // (RabbitMqPublisher.CreateAsync) ANTES de builder.Build() — ou seja, antes do ponto em
        // que WebApplicationFactory.ConfigureWebHost consegue injetar config (esse mecanismo só
        // alcança o que é lido via DI depois do host construído). Por isso, a única forma de a
        // API de teste enxergar os containers reais é via variável de ambiente, lida já dentro
        // de WebApplication.CreateBuilder(args) — antes de qualquer linha do Program.cs rodar.
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", PostgresConnectionString);
        Environment.SetEnvironmentVariable("RabbitMq__Host", RabbitMqHost);
        Environment.SetEnvironmentVariable("RabbitMq__Port", RabbitMqPort.ToString());
        Environment.SetEnvironmentVariable("RabbitMq__Username", "guest");
        Environment.SetEnvironmentVariable("RabbitMq__Password", "guest");
        Environment.SetEnvironmentVariable("Outbox__IntervalSeconds", "1");

        await using (var apiContext = CreateApiDbContext())
        {
            await apiContext.Database.MigrateAsync();
        }

        await using (var workerContext = CreateWorkerDbContext())
        {
            await workerContext.Database.MigrateAsync();
        }

        // Subir a API primeiro faz com que RabbitMqTopologyInitializer declare a topologia
        // completa (exchanges, filas principais, DLQ e retry) exatamente como em produção — os
        // testes não reimplementam essa declaração.
        ApiFactory = new NotifyFlowApiFactory(PostgresConnectionString, RabbitMqHost, RabbitMqPort);
        using (ApiFactory.CreateClient())
        {
        }

        var factory = new ConnectionFactory
        {
            HostName = RabbitMqHost,
            Port = RabbitMqPort,
            UserName = "guest",
            Password = "guest"
        };
        _rabbitConnection = await factory.CreateConnectionAsync();
    }

    public async Task DisposeAsync()
    {
        await _rabbitConnection.DisposeAsync();
        ApiFactory.Dispose();
        await _rabbitMq.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public NotifyFlowApiDbContext CreateApiDbContext()
    {
        var options = new DbContextOptionsBuilder<NotifyFlowApiDbContext>()
            .UseNpgsql(PostgresConnectionString)
            .Options;
        return new NotifyFlowApiDbContext(options);
    }

    public NotifyFlowWorkerDbContext CreateWorkerDbContext()
    {
        var options = new DbContextOptionsBuilder<NotifyFlowWorkerDbContext>()
            .UseNpgsql(PostgresConnectionString)
            .Options;
        return new NotifyFlowWorkerDbContext(options);
    }

    public async Task<IChannel> CreateRabbitChannelAsync() => await _rabbitConnection.CreateChannelAsync();

    public NotifyFlow.Worker.Messaging.RabbitMqOptions CreateWorkerRabbitMqOptions() => new()
    {
        Host = RabbitMqHost,
        Port = RabbitMqPort,
        Username = "guest",
        Password = "guest",
        ExchangeName = "notifyflow.events",
        RoutingKey = "notifications",
        QueueName = "notifyflow.notifications",
        DeadLetterExchangeName = "notifyflow.events.dlx",
        DeadLetterQueueName = "notifyflow.notifications.dlq",
        PrefetchCount = 1
    };
}
