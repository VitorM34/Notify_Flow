namespace NotifyFlow.Api.Persistence.Entities;

/// <summary>
/// Histórico do evento recebido pela API, persistido antes/junto da publicação.
/// </summary>
public sealed class StoredEvent
{
    /// <summary>Chave primária interna (bigint identity), independente do EventId público.</summary>
    public long Id { get; set; }

    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public int Version { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string Source { get; set; } = string.Empty;

    public Guid CorrelationId { get; set; }

    /// <summary>Payload serializado como JSON, para auditoria/rastreabilidade.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
