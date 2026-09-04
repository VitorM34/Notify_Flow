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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectAsync(stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel!);

        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(args.Body.Span);
                var envelope = JsonSerializer.Deserialize<EventEnvelope>(json);

                if (envelope is null)
                {
                    _logger.LogWarning("Received null envelope, discarding message");
                    await _channel!.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                _logger.LogInformation(
                    "Message received | EventType: {EventType} | EventId: {EventId}",
                    envelope.EventType, envelope.EventId);

                await _dispatcher.DispatchAsync(envelope, stoppingToken);

                await _channel!.BasicAckAsync(args.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message. DeliveryTag: {DeliveryTag}", args.DeliveryTag);
                await _channel!.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await _channel!.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

        await _channel!.BasicConsumeAsync(
            queue: "notifyflow.notifications",
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

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        _logger.LogInformation("Connected to RabbitMQ");
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
