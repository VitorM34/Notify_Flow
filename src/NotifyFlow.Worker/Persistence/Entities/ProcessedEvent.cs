namespace NotifyFlow.Worker.Persistence.Entities;

/// <summary>
/// Marcador de "este evento já foi processado por este consumidor" — base da E05-T03
/// (idempotent consumer). A combinação (EventId, ConsumerName) é única: se a mesma mensagem
/// chegar duplicada (redelivery do RabbitMQ, por exemplo), o Worker consegue detectar e pular
/// o reprocessamento em vez de executar o handler de novo.
/// </summary>
public sealed class ProcessedEvent
{
    public long Id { get; set; }

    public Guid EventId { get; set; }

    public string ConsumerName { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; set; }
}
