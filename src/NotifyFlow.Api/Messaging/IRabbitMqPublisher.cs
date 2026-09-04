using NotifyFlow.Contracts;

namespace NotifyFlow.Api.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default);
}
