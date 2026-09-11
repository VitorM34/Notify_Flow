using NotifyFlow.Contracts;

namespace NotifyFlow.Api.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishAsync(EventMessage message, CancellationToken cancellationToken = default);
}
