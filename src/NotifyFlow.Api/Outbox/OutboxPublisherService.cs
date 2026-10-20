using Microsoft.Extensions.Options;

namespace NotifyFlow.Api.Outbox;

/// <summary>
/// Agenda a execução periódica de <see cref="IOutboxPublisher.PublishPendingAsync"/> (E04-T03).
/// Padrão de "timed background task" com <see cref="Task.Delay(TimeSpan, CancellationToken)"/>
/// — ver https://learn.microsoft.com/en-us/dotnet/core/extensions/workers.
/// </summary>
public sealed class OutboxPublisherService : BackgroundService
{
    private readonly IOutboxPublisher _outboxPublisher;
    private readonly OutboxPublisherOptions _options;
    private readonly ILogger<OutboxPublisherService> _logger;

    public OutboxPublisherService(
        IOutboxPublisher outboxPublisher,
        IOptions<OutboxPublisherOptions> options,
        ILogger<OutboxPublisherService> logger)
    {
        _outboxPublisher = outboxPublisher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _outboxPublisher.PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Cancelamento esperado durante shutdown do host — não é uma falha de publicação.
                break;
            }
            catch (Exception ex)
            {
                // Uma falha de publicação (ou de acesso ao banco) não pode derrubar o loop de
                // polling: logamos e tentamos de novo no próximo ciclo.
                _logger.LogError(ex, "Falha inesperada ao processar lote da outbox");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
