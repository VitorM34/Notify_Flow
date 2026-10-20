namespace NotifyFlow.Api.Persistence.Entities;

/// <summary>
/// Mensagem pendente (ou já processada) de publicação no RabbitMQ, persistida na
/// mesma transação do StoredEvent (Transactional Outbox — implementado na E04-T02/E04-T03).
/// </summary>
public sealed class OutboxMessage
{
    public long Id { get; set; }

    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string RoutingKey { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>Ver <see cref="OutboxMessageStatus"/> para os valores possíveis.</summary>
    public string Status { get; set; } = OutboxMessageStatus.Pending;

    public int AttemptCount { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
