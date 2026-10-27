using NotifyFlow.Contracts;

namespace NotifyFlow.Api.Observability;

public static class LogContextExtensions
{
    public static IDisposable? BeginEventScope(this ILogger logger, EventMessage message)
    {
        return logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new("EventId", message.EventId),
            new("CorrelationId", message.CorrelationId),
            new("EventType", message.EventType)
        });
    }
}
