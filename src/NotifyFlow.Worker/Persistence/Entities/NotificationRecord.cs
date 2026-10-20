namespace NotifyFlow.Worker.Persistence.Entities;

/// <summary>
/// Registro de uma notificação processada pelo Worker (resultado de consumir um evento do
/// RabbitMQ e executar o handler correspondente) — parte da fundação de persistência da
/// E04-T01. Usado futuramente para auditoria/consulta e, junto com <see cref="ProcessedEvent"/>,
/// para a E05-T03 (idempotência).
/// </summary>
public sealed class NotificationRecord
{
    public long Id { get; set; }

    public Guid EventId { get; set; }

    public Guid CorrelationId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Recipient { get; set; } = string.Empty;

    /// <summary>Ver <see cref="NotificationStatus"/> para os valores possíveis.</summary>
    public string Status { get; set; } = string.Empty;

    public string? Provider { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
