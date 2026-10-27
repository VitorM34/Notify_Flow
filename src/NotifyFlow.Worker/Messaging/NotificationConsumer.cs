using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotifyFlow.Contracts;
using NotifyFlow.Worker.Failures;
using NotifyFlow.Worker.Handlers;
using NotifyFlow.Worker.Persistence;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotifyFlow.Worker.Messaging;

public sealed class NotificationConsumer : BackgroundService
{
    private const string ConsumerName = "NotificationConsumer";

    private readonly RabbitMqOptions _options;
    private readonly RabbitMqTopology _topology;
    private readonly HandlerDispatcher _dispatcher;
    private readonly IFailureClassifier _failureClassifier;
    private readonly RetryPolicyOptions _retryPolicy;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public NotificationConsumer(
        IOptions<RabbitMqOptions> options,
        HandlerDispatcher dispatcher,
        IFailureClassifier failureClassifier,
        IOptions<RetryPolicyOptions> retryPolicy,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationConsumer> logger)
    {
        _options = options.Value;
        _topology = new RabbitMqTopology(_options);
        _dispatcher = dispatcher;
        _failureClassifier = failureClassifier;
        _retryPolicy = retryPolicy.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectAsync(stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel!);

        consumer.ReceivedAsync += async (_, args) =>
        {
            EventMessage? message = null;

            try
            {
                var json = Encoding.UTF8.GetString(args.Body.Span);
                message = JsonSerializer.Deserialize<EventMessage>(json);

                if (message is null)
                {
                    _logger.LogWarning("Received null message, discarding message");
                    await _channel!.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                _logger.LogInformation(
                    "Message received | EventType: {EventType} | EventId: {EventId} | CorrelationId: {CorrelationId}",
                    message.EventType, message.EventId, message.CorrelationId);

                using (var scope = _scopeFactory.CreateScope())
                {
                    var processedEventStore = scope.ServiceProvider.GetRequiredService<IProcessedEventStore>();

                    if (await processedEventStore.HasProcessedAsync(message.EventId, ConsumerName, stoppingToken))
                    {
                        _logger.LogInformation(
                            "Evento já processado por este consumidor — pulando reprocessamento (idempotência) | EventId: {EventId} | CorrelationId: {CorrelationId}",
                            message.EventId, message.CorrelationId);

                        await _channel!.BasicAckAsync(args.DeliveryTag, multiple: false);
                        return;
                    }

                    await _dispatcher.DispatchAsync(message, stoppingToken);

                    await processedEventStore.MarkProcessedAsync(message.EventId, ConsumerName, DateTimeOffset.UtcNow, stoppingToken);
                }

                await _channel!.BasicAckAsync(args.DeliveryTag, multiple: false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Processamento interrompido por shutdown do Worker. DeliveryTag: {DeliveryTag}", args.DeliveryTag);
            }
            catch (Exception ex)
            {
                await HandleProcessingFailureAsync(args, message, ex);
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

    private async Task HandleProcessingFailureAsync(BasicDeliverEventArgs args, EventMessage? message, Exception exception)
    {
        var classification = _failureClassifier.Classify(exception, message);
        var currentAttempt = GetAttemptCount(args);

        if (classification == FailureClassification.Transient && currentAttempt < _retryPolicy.MaxAttempts)
        {
            var nextAttempt = currentAttempt + 1;

            _logger.LogWarning(
                exception,
                "Falha transitória | EventId: {EventId} | CorrelationId: {CorrelationId} | Classificação: {Classification} | Tentativa: {Attempt}/{MaxAttempts} | Destino: retry queue {NextAttempt}",
                message?.EventId, message?.CorrelationId, classification, nextAttempt, _retryPolicy.MaxAttempts, nextAttempt);

            await PublishToRetryQueueAsync(args.Body, nextAttempt);
            await _channel!.BasicAckAsync(args.DeliveryTag, multiple: false);
            return;
        }

        var reason = classification == FailureClassification.Transient
            ? "tentativas esgotadas"
            : classification.ToString();

        _logger.LogError(
            exception,
            "Falha ao processar mensagem | EventId: {EventId} | CorrelationId: {CorrelationId} | Classificação: {Classification} | Tentativa: {Attempt} | Destino: DLQ ({Reason})",
            message?.EventId, message?.CorrelationId, classification, currentAttempt, reason);

        await _channel!.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
    }

    private static int GetAttemptCount(BasicDeliverEventArgs args)
    {
        var headers = args.BasicProperties.Headers;

        if (headers is not null
            && headers.TryGetValue(RetryHeaders.AttemptCount, out var raw)
            && raw is not null
            && int.TryParse(raw.ToString(), out var attempt))
        {
            return attempt;
        }

        return 0;
    }

    private async Task PublishToRetryQueueAsync(ReadOnlyMemory<byte> body, int attempt)
    {
        var (queueName, routingKey) = attempt switch
        {
            1 => (RabbitMqRetryTopology.RetryQueueName1, RabbitMqRetryTopology.RetryRoutingKey1),
            2 => (RabbitMqRetryTopology.RetryQueueName2, RabbitMqRetryTopology.RetryRoutingKey2),
            3 => (RabbitMqRetryTopology.RetryQueueName3, RabbitMqRetryTopology.RetryRoutingKey3),
            _ => throw new ArgumentOutOfRangeException(nameof(attempt), attempt, $"Não existe retry queue para a tentativa {attempt} (MaxAttempts deveria ter barrado isso antes).")
        };

        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?>
            {
                [RetryHeaders.AttemptCount] = attempt
            }
        };

        await _channel!.BasicPublishAsync(
            exchange: RabbitMqRetryTopology.RetryExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);

        _logger.LogInformation("Mensagem publicada na retry queue {QueueName} | Tentativa: {Attempt}", queueName, attempt);
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

        await channel.ExchangeDeclareAsync(
            exchange: RabbitMqRetryTopology.RetryExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await DeclareRetryQueueAsync(
            channel, RabbitMqRetryTopology.RetryQueueName1, RabbitMqRetryTopology.RetryRoutingKey1, RabbitMqRetryTopology.RetryTtlMs1, cancellationToken);
        await DeclareRetryQueueAsync(
            channel, RabbitMqRetryTopology.RetryQueueName2, RabbitMqRetryTopology.RetryRoutingKey2, RabbitMqRetryTopology.RetryTtlMs2, cancellationToken);
        await DeclareRetryQueueAsync(
            channel, RabbitMqRetryTopology.RetryQueueName3, RabbitMqRetryTopology.RetryRoutingKey3, RabbitMqRetryTopology.RetryTtlMs3, cancellationToken);
    }

    private async Task DeclareRetryQueueAsync(IChannel channel, string queueName, string retryRoutingKey, int ttlMs, CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = ttlMs,
                ["x-dead-letter-exchange"] = _topology.ExchangeName,
                ["x-dead-letter-routing-key"] = _topology.RoutingKey
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: RabbitMqRetryTopology.RetryExchangeName,
            routingKey: retryRoutingKey,
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
