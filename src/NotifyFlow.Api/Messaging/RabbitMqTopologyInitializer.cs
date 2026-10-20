using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace NotifyFlow.Api.Messaging;

public sealed class RabbitMqTopologyInitializer : IRabbitMqTopologyInitializer
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqTopologyInitializer> _logger;

    public RabbitMqTopologyInitializer(IOptions<RabbitMqOptions> options, ILogger<RabbitMqTopologyInitializer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var topology = new RabbitMqTopology(_options);

        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: topology.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: topology.DeadLetterExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: topology.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: topology.DeadLetterQueueName,
            exchange: topology.DeadLetterExchangeName,
            routingKey: topology.RoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: topology.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = topology.DeadLetterExchangeName
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: topology.QueueName,
            exchange: topology.ExchangeName,
            routingKey: topology.RoutingKey,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: RabbitMqRetryTopology.RetryExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await DeclareRetryQueueAsync(
            channel, topology, RabbitMqRetryTopology.RetryQueueName1, RabbitMqRetryTopology.RetryRoutingKey1, RabbitMqRetryTopology.RetryTtlMs1, cancellationToken);
        await DeclareRetryQueueAsync(
            channel, topology, RabbitMqRetryTopology.RetryQueueName2, RabbitMqRetryTopology.RetryRoutingKey2, RabbitMqRetryTopology.RetryTtlMs2, cancellationToken);
        await DeclareRetryQueueAsync(
            channel, topology, RabbitMqRetryTopology.RetryQueueName3, RabbitMqRetryTopology.RetryRoutingKey3, RabbitMqRetryTopology.RetryTtlMs3, cancellationToken);

        _logger.LogInformation(
            "Topologia RabbitMQ inicializada | Exchange: {Exchange} | Queue: {Queue} | RoutingKey: {RoutingKey} | DeadLetterExchange: {DeadLetterExchange} | DeadLetterQueue: {DeadLetterQueue} | RetryExchange: {RetryExchange}",
            topology.ExchangeName, topology.QueueName, topology.RoutingKey, topology.DeadLetterExchangeName, topology.DeadLetterQueueName, RabbitMqRetryTopology.RetryExchangeName);
    }

    private static async Task DeclareRetryQueueAsync(
        IChannel channel,
        RabbitMqTopology topology,
        string queueName,
        string retryRoutingKey,
        int ttlMs,
        CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = ttlMs,
                ["x-dead-letter-exchange"] = topology.ExchangeName,
                ["x-dead-letter-routing-key"] = topology.RoutingKey
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: RabbitMqRetryTopology.RetryExchangeName,
            routingKey: retryRoutingKey,
            cancellationToken: cancellationToken);
    }
}
