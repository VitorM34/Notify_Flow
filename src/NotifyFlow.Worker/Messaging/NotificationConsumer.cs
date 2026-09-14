using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotifyFlow.Contracts;
using NotifyFlow.Worker.Handlers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotifyFlow.Worker.Messaging;

public sealed class NotificationConsumer : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly HandlerDispatcher _dispatcher;
    private readonly ILogger<NotificationConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public NotificationConsumer(
        IConfiguration configuration,
        HandlerDispatcher dispatcher,
        ILogger<NotificationConsumer> logger)
    {
        _configuration = configuration;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private string QueueName => _configuration["RabbitMq:QueueName"]!;

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

        await _channel!.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

        await _channel!.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMq:Host"]!,
            Port = int.Parse(_configuration["RabbitMq:Port"]!),
            UserName = _configuration["RabbitMq:Username"]!,
            Password = _configuration["RabbitMq:Password"]!
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
        var exchangeName = _configuration["RabbitMq:ExchangeName"]!;
        var routingKey = _configuration["RabbitMq:RoutingKey"]!;
        var deadLetterExchangeName = _configuration["RabbitMq:DeadLetterExchangeName"]!;
        var deadLetterQueueName = _configuration["RabbitMq:DeadLetterQueueName"]!;

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: deadLetterExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: deadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: deadLetterQueueName,
            exchange: deadLetterExchangeName,
            routingKey: routingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = deadLetterExchangeName
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: QueueName,
            exchange: exchangeName,
            routingKey: routingKey,
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
