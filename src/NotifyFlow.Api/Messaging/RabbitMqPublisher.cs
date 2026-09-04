using System.Text;
using System.Text.Json;
using NotifyFlow.Contracts;
using RabbitMQ.Client;

namespace NotifyFlow.Api.Messaging;

public sealed class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _exchangeName;
    private readonly string _routingKey;

    private RabbitMqPublisher(IConnection connection, IChannel channel, string exchangeName, string routingKey)
    {
        _connection = connection;
        _channel = channel;
        _exchangeName = exchangeName;
        _routingKey = routingKey;
    }

    public static async Task<RabbitMqPublisher> CreateAsync(IConfiguration configuration)
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

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);

        await channel.QueueDeclareAsync(
            queue: "notifyflow.notifications",
            durable: true,
            exclusive: false,
            autoDelete: false);

        await channel.QueueBindAsync(
            queue: "notifyflow.notifications",
            exchange: exchangeName,
            routingKey: configuration["RabbitMq:RoutingKey"]!);

        return new RabbitMqPublisher(connection, channel, exchangeName, configuration["RabbitMq:RoutingKey"]!);
    }

    public async Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(envelope);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            ContentType = "application/json",
            MessageId = envelope.EventId.ToString(),
            CorrelationId = envelope.CorrelationId.ToString()
        };

        await _channel.BasicPublishAsync(
            exchange: _exchangeName,
            routingKey: _routingKey,
            mandatory: false,
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
