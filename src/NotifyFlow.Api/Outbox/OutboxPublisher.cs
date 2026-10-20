using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NotifyFlow.Api.Messaging;
using NotifyFlow.Api.Persistence;
using NotifyFlow.Api.Persistence.Entities;

namespace NotifyFlow.Api.Outbox;

/// <summary>
/// Implementação do caso de uso "publicar lote pendente da outbox" (E04-T03).
/// </summary>
public sealed class OutboxPublisher : IOutboxPublisher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRabbitMqPublisher _rabbitMqPublisher;
    private readonly OutboxPublisherOptions _options;
    private readonly ILogger<OutboxPublisher> _logger;

    // OutboxPublisherService (BackgroundService) é Singleton, mas NotifyFlowApiDbContext é
    // Scoped — por isso injetamos IServiceScopeFactory em vez do DbContext diretamente, e
    // criamos um escopo novo a cada ciclo de PublishPendingAsync (evita o problema de
    // "captive dependency": um Scoped vivendo pelo tempo de vida de um Singleton).
    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        IRabbitMqPublisher rabbitMqPublisher,
        IOptions<OutboxPublisherOptions> options,
        ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _rabbitMqPublisher = rabbitMqPublisher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotifyFlowApiDbContext>();

        // Risco de concorrência conhecido e documentado (E04-T03, Passo 7, item "Fora do
        // escopo" da task): se mais de uma instância da API rodar simultaneamente, duas
        // instâncias podem ler o mesmo lote Pending e publicar a mesma mensagem duas vezes
        // (não há SELECT ... FOR UPDATE SKIP LOCKED nem lock distribuído aqui). Para a V1
        // local, com uma única instância da API, isso não acontece na prática — mas o fluxo
        // já é at-least-once por natureza (publisher confirm garante só que o broker recebeu,
        // não que o Worker processou uma única vez), então um consumidor idempotente no
        // Worker (via ProcessedEvent) é a defesa correta contra duplicidade, não um lock aqui.
        var batch = await dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (batch.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var message in batch)
        {
            try
            {
                // Só consideramos sucesso depois que este await retornar sem lançar: com
                // publisher confirms habilitados em RabbitMqPublisher (CreateChannelOptions),
                // PublishRawAsync só completa depois que o broker confirma (ack) o
                // recebimento/persistência da mensagem. Um nack ou um basic.return (mensagem
                // não roteada) faz o client lançar uma exceção, capturada abaixo como falha.
                await _rabbitMqPublisher.PublishRawAsync(message.RoutingKey, message.PayloadJson, message.EventId, cancellationToken);

                message.Status = OutboxMessageStatus.Published;
                message.PublishedAt = now;
                message.LastAttemptAt = now;
                message.Error = null;

                _logger.LogInformation(
                    "OutboxMessage publicada | EventId: {EventId} | EventType: {EventType} | RoutingKey: {RoutingKey}",
                    message.EventId, message.EventType, message.RoutingKey);
            }
            catch (Exception ex)
            {
                message.AttemptCount += 1;
                message.LastAttemptAt = now;
                message.Error = ex.Message;

                // Decisão arquitetural (documentada também em OutboxPublisherOptions.MaxAttempts):
                // ao atingir o limite de tentativas, desistimos da mensagem e a marcamos como
                // Failed, em vez de deixá-la Pending para sempre. Isso é uma escolha deliberada
                // de V1 — não existe aqui um mecanismo de reprocessamento manual de mensagens
                // Failed, isso fica fora do escopo desta task.
                if (message.AttemptCount >= _options.MaxAttempts)
                {
                    message.Status = OutboxMessageStatus.Failed;
                }

                _logger.LogWarning(
                    ex,
                    "Falha ao publicar OutboxMessage | EventId: {EventId} | AttemptCount: {AttemptCount} | Status: {Status}",
                    message.EventId, message.AttemptCount, message.Status);
            }
        }

        // Uma única SaveChangesAsync após o lote inteiro: granularidade escolhida para reduzir
        // round-trips ao banco por ciclo (o lote já é pequeno, limitado por BatchSize). Cada
        // OutboxMessage é uma linha independente, então uma eventual falha de SaveChanges não
        // mistura o estado de mensagens não relacionadas entre si além de não persistir o ciclo.
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
