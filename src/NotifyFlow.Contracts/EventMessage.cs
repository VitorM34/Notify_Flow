namespace NotifyFlow.Contracts;

public sealed record EventMessage
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public required string EventType { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required string Source { get; init; }
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public int Version { get; init; } = 1;
    public required object Payload { get; init; }
}
