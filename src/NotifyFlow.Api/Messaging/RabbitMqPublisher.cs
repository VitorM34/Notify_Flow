using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NotifyFlow.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotifyFlow.Api.Messaging;

public sealed class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly RabbitMqTopology _topology;
    private readonly ILogger<RabbitMqPublisher> _logger;

    private RabbitMqPublisher(
        IConnection connection,
        IChannel channel,
        RabbitMqTopology topology,
        ILogger<RabbitMqPublisher> logger)
    {
        _connection = connection;
        _channel = channel;
        _topology = topology;
        _logger = logger;
    }

    public static async Task<RabbitMqPublisher> CreateAsync(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger)
    {
        var rabbitMqOptions = options.Value;
        var topology = new RabbitMqTopology(rabbitMqOptions);

        var factory = new ConnectionFactory
        {
            HostName = rabbitMqOptions.Host,
            Port = rabbitMqOptions.Port,
            UserName = rabbitMqOptions.Username,
            Password = rabbitMqOptions.Password
        };

        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        channel.BasicReturnAsync += (_, args) =>
        {
            logger.LogWarning(
                "Mensagem não roteada devolvida pelo broker | ReplyCode: {ReplyCode} | ReplyText: {ReplyText} | Exchange: {Exchange} | RoutingKey: {RoutingKey}",
                args.ReplyCode, args.ReplyText, args.Exchange, args.RoutingKey);

            return Task.CompletedTask;
        };

        return new RabbitMqPublisher(connection, channel, topology, logger);
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
            exchange: _topology.ExchangeName,
            routingKey: _topology.RoutingKey,
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
