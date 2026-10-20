using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotifyFlow.Worker.Persistence.Entities;

namespace NotifyFlow.Worker.Persistence;

public sealed class ProcessedEventStore : IProcessedEventStore
{
    private readonly NotifyFlowWorkerDbContext _dbContext;
    private readonly ILogger<ProcessedEventStore> _logger;

    public ProcessedEventStore(NotifyFlowWorkerDbContext dbContext, ILogger<ProcessedEventStore> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task<bool> HasProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProcessedEvents
            .AnyAsync(p => p.EventId == eventId && p.ConsumerName == consumerName, cancellationToken);
    }

    public async Task MarkProcessedAsync(Guid eventId, string consumerName, DateTimeOffset processedAt, CancellationToken cancellationToken = default)
    {
        _dbContext.ProcessedEvents.Add(new ProcessedEvent
        {
            EventId = eventId,
            ConsumerName = consumerName,
            ProcessedAt = processedAt
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Violação da constraint única (EventId, ConsumerName): outra instância já marcou este evento, não é erro fatal.
            _logger.LogWarning(
                ex,
                "MarkProcessedAsync colidiu com um registro já existente (corrida de concorrência) | EventId: {EventId} | ConsumerName: {ConsumerName}",
                eventId, consumerName);
        }
    }
}
