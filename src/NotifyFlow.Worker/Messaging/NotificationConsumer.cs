using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotifyFlow.Contracts;
using NotifyFlow.Worker.Handlers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotifyFlow.Worker.Messaging;

public sealed class NotificationConsumer : BackgroundService
{
    private readonly RabbitMqOptions _options;
    private readonly RabbitMqTopology _topology;
    private readonly HandlerDispatcher _dispatcher;
    private readonly ILogger<NotificationConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public NotificationConsumer(
        IOptions<RabbitMqOptions> options,
        HandlerDispatcher dispatcher,
        ILogger<NotificationConsumer> logger)
    {
        _options = options.Value;
        _topology = new RabbitMqTopology(_options);
        _dispatcher = dispatcher;
        _logger = logger;
    }

    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectAsync(stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel!);

        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(args.Body.Span);
                var message = JsonSerializer.Deserialize<EventMessage>(json);

                if (message is null)
                {
                    _logger.LogWarning("Received null message, discarding message");
                    await _channel!.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                _logger.LogInformation(
                    "Message received | EventType: {EventType} | EventId: {EventId}",
                    message.EventType, message.EventId);

                await _dispatcher.DispatchAsync(message, stoppingToken);

                await _channel!.BasicAckAsync(args.DeliveryTag, multiple: false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Processamento interrompido por shutdown do Worker. DeliveryTag: {DeliveryTag}", args.DeliveryTag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message. DeliveryTag: {DeliveryTag}", args.DeliveryTag);
                await _channel!.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await _channel!.BasicQosAsync(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false);

        await _channel!.BasicConsumeAsync(
            queue: _topology.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password
        };

        var attempt = 0;

        while (true)
        {
            try
            {
                attempt++;

                _connection = await factory.CreateConnectionAsync(cancellationToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

                await DeclareTopologyAsync(_channel, cancellationToken);

                _logger.LogInformation("Connected to RabbitMQ");
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), MaxRetryDelay.TotalSeconds));

                _logger.LogError(
                    ex,
                    "Falha ao conectar no RabbitMQ (tentativa {Attempt}). Nova tentativa em {Delay}s",
                    attempt, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            exchange: _topology.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: _topology.DeadLetterExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _topology.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _topology.DeadLetterQueueName,
            exchange: _topology.DeadLetterExchangeName,
            routingKey: _topology.RoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _topology.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _topology.DeadLetterExchangeName
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _topology.QueueName,
            exchange: _topology.ExchangeName,
            routingKey: _topology.RoutingKey,
            cancellationToken: cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_channel is not null)
            await _channel.DisposeAsync();

        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
