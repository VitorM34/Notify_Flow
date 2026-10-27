using System.Text.Json;
using NotifyFlow.Api.Observability;
using NotifyFlow.Api.Persistence;
using NotifyFlow.Api.Persistence.Entities;

namespace NotifyFlow.Api.Features.Events.CreateEvent;

/// <summary>
/// Implementação do caso de uso de recebimento de evento (E04-T02). Grava <c>StoredEvent</c> e
/// <c>OutboxMessage</c> no mesmo <see cref="NotifyFlowApiDbContext"/> e numa única chamada a
/// <see cref="DbContext.SaveChangesAsync"/>, removendo a dependência de o RabbitMQ estar de pé
/// para o request principal responder.
/// </summary>
public sealed class CreateEventService : ICreateEventService
{
    private readonly NotifyFlowApiDbContext _dbContext;
    private readonly ILogger<CreateEventService> _logger;

    public CreateEventService(NotifyFlowApiDbContext dbContext, ILogger<CreateEventService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<EventAcceptedResponse> CreateAsync(CreateEventCommand command, CancellationToken cancellationToken = default)
    {
        var message = command.Message;
        var now = DateTimeOffset.UtcNow;

        var storedEvent = new StoredEvent
        {
            EventId = message.EventId,
            EventType = message.EventType,
            Version = message.Version,
            OccurredAt = message.OccurredAt,
            Source = message.Source,
            CorrelationId = message.CorrelationId,
            // Aqui guardamos só o payload de domínio (ex.: UserRegisteredEvent) — os demais
            // campos do envelope (EventType, Version, OccurredAt, Source, CorrelationId) já têm
            // colunas próprias em StoredEvent, então serializar o EventMessage inteiro de novo
            // seria redundante.
            PayloadJson = JsonSerializer.Serialize(message.Payload),
            CreatedAt = now
        };

        var outboxMessage = new OutboxMessage
        {
            EventId = message.EventId,
            EventType = message.EventType,
            RoutingKey = command.RoutingKey,
            // Diferente do StoredEvent, a outbox não tem colunas próprias para o envelope
            // (OccurredAt/Source/CorrelationId/Version) — ela serializa o EventMessage completo
            // porque é esse JSON, bytes-a-bytes, que o Outbox Publisher (E04-T03) vai publicar
            // como corpo da mensagem no RabbitMQ.
            PayloadJson = JsonSerializer.Serialize(message),
            Status = OutboxMessageStatus.Pending,
            AttemptCount = 0,
            CreatedAt = now
        };

        // Uma única chamada a SaveChangesAsync cobrindo os dois Add já é atômica: por padrão,
        // o EF Core envolve todas as mudanças de uma mesma chamada a SaveChanges numa transação
        // implícita (AutoTransactionBehavior.WhenNeeded — ver
        // https://learn.microsoft.com/en-us/ef/core/saving/transactions#default-transaction-behavior).
        // Não abrimos uma transação explícita (Database.BeginTransactionAsync) de propósito:
        // só faria sentido se precisássemos de múltiplas chamadas a SaveChangesAsync, de uma
        // query no meio do caminho, ou de compartilhar a transação com outro DbContext — nada
        // disso acontece aqui.
        _dbContext.StoredEvents.Add(storedEvent);
        _dbContext.OutboxMessages.Add(outboxMessage);

        await _dbContext.SaveChangesAsync(cancellationToken);

        using var _ = _logger.BeginEventScope(message);
        _logger.LogInformation(
            "Evento persistido na outbox | EventId: {EventId} | CorrelationId: {CorrelationId} | RoutingKey: {RoutingKey}",
            message.EventId, message.CorrelationId, command.RoutingKey);

        return new EventAcceptedResponse(message.EventId, message.CorrelationId);
    }
}
