using Microsoft.Extensions.Logging;
using NotifyFlow.Contracts;

namespace NotifyFlow.Worker.Handlers;

public sealed class HandlerDispatcher
{
    private readonly IReadOnlyDictionary<string, INotificationHandler> _handlers;
    private readonly ILogger<HandlerDispatcher> _logger;

    public HandlerDispatcher(
        IEnumerable<INotificationHandler> handlers,
        ILogger<HandlerDispatcher> logger)
    {
        _handlers = handlers.ToDictionary(h => h.EventType, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public async Task DispatchAsync(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(envelope.EventType, out var handler))
        {
            _logger.LogWarning("No handler found for event type {EventType}", envelope.EventType);
            return;
        }

        await handler.HandleAsync(envelope, cancellationToken);
    }
}
