using NotifyFlow.Contracts;

namespace NotifyFlow.Worker.Handlers;

public interface INotificationHandler
{
    string EventType { get; }
    Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken = default);
}
