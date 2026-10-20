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

        // Publisher confirms (E04-T03): habilitados via CreateChannelOptions, conforme a API
        // atual do RabbitMQ.Client v7 — ver https://www.rabbitmq.com/client-libraries/dotnet-api-guide
        // (seção "Publisher Confirms") e https://www.rabbitmq.com/tutorials/tutorial-seven-dotnet.
        // Com publisherConfirmationsEnabled + publisherConfirmationTrackingEnabled ligados, o
        // próprio client passa a aguardar o ack/nack do broker dentro do await a BasicPublishAsync
        // (estratégia "individual publishing" do tutorial 7) — se a mensagem for nack-ada ou
        // devolvida (returned), o método lança exceção em vez de retornar normalmente.
        var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true));

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

    public async Task PublishRawAsync(string routingKey, string payloadJson, Guid eventId, CancellationToken cancellationToken = default)
    {
        var body = Encoding.UTF8.GetBytes(payloadJson);

        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            ContentType = "application/json",
            MessageId = eventId.ToString()
        };

        // Com confirms habilitados no channel (ver CreateAsync), este await só retorna depois
        // que o broker confirmar (ack) o recebimento/persistência da mensagem — se o broker
        // nack-ar ou devolver (basic.return, dado mandatory: true) a mensagem, uma exceção é
        // lançada aqui e propagada para o OutboxPublisher, que trata como falha de publicação.
        await _channel.BasicPublishAsync(
            exchange: _topology.ExchangeName,
            routingKey: routingKey,
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
