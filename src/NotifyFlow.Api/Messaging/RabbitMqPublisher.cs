using System.Text;
using System.Text.Json;
using NotifyFlow.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotifyFlow.Api.Messaging;

public sealed class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _exchangeName;
    private readonly string _routingKey;
    private readonly ILogger<RabbitMqPublisher> _logger;

    private RabbitMqPublisher(
        IConnection connection,
        IChannel channel,
        string exchangeName,
        string routingKey,
        ILogger<RabbitMqPublisher> logger)
    {
        _connection = connection;
        _channel = channel;
        _exchangeName = exchangeName;
        _routingKey = routingKey;
        _logger = logger;
    }

    public static async Task<RabbitMqPublisher> CreateAsync(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    {
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMq:Host"]!,
            Port = int.Parse(configuration["RabbitMq:Port"]!),
            UserName = configuration["RabbitMq:Username"]!,
            Password = configuration["RabbitMq:Password"]!
        };

        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        var exchangeName = configuration["RabbitMq:ExchangeName"]!;
        var routingKey = configuration["RabbitMq:RoutingKey"]!;
        var queueName = configuration["RabbitMq:QueueName"]!;
        var deadLetterExchangeName = configuration["RabbitMq:DeadLetterExchangeName"]!;
        var deadLetterQueueName = configuration["RabbitMq:DeadLetterQueueName"]!;

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);

        await channel.ExchangeDeclareAsync(
            exchange: deadLetterExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);

        await channel.QueueDeclareAsync(
            queue: deadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        await channel.QueueBindAsync(
            queue: deadLetterQueueName,
            exchange: deadLetterExchangeName,
            routingKey: routingKey);

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = deadLetterExchangeName
            });

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: routingKey);

        channel.BasicReturnAsync += (_, args) =>
        {
            logger.LogWarning(
                "Mensagem não roteada devolvida pelo broker | ReplyCode: {ReplyCode} | ReplyText: {ReplyText} | Exchange: {Exchange} | RoutingKey: {RoutingKey}",
                args.ReplyCode, args.ReplyText, args.Exchange, args.RoutingKey);

            return Task.CompletedTask;
        };

        return new RabbitMqPublisher(connection, channel, exchangeName, routingKey, logger);
    }

    public async Task PublishAsync(EventMessage message, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            ContentType = "application/json",
            MessageId = message.EventId.ToString(),
            CorrelationId = message.CorrelationId.ToString()
        };

        await _channel.BasicPublishAsync(
            exchange: _exchangeName,
            routingKey: _routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
