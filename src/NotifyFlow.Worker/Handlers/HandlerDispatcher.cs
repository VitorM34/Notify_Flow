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

    public async Task DispatchAsync(EventMessage message, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(message.EventType, out var handler))
        {
            _logger.LogWarning("No handler found for event type {EventType}", message.EventType);
            return;
        }

        await handler.HandleAsync(message, cancellationToken);
    }
}
