using NotifyFlow.Contracts;

namespace NotifyFlow.Worker.Handlers;

public interface INotificationHandler
{
    string EventType { get; }
    Task HandleAsync(EventMessage message, CancellationToken cancellationToken = default);
}
