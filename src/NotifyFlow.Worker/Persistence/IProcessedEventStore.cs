namespace NotifyFlow.Worker.Persistence;

public interface IProcessedEventStore
{
    Task<bool> HasProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(Guid eventId, string consumerName, DateTimeOffset processedAt, CancellationToken cancellationToken = default);
}
