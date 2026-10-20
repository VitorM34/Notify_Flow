using NotifyFlow.Contracts;

namespace NotifyFlow.Api.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishAsync(EventMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publica um payload já serializado (o JSON bytes-a-bytes gravado em
    /// <c>OutboxMessage.PayloadJson</c>) para a <paramref name="routingKey"/> informada — usado
    /// pelo Outbox Publisher (E04-T03), que não tem mais um <see cref="EventMessage"/> em mãos,
    /// apenas o que já foi persistido. Mantido como método separado de <see cref="PublishAsync"/>
    /// para não alterar o comportamento/contrato existente deste último.
    /// </summary>
    Task PublishRawAsync(string routingKey, string payloadJson, Guid eventId, CancellationToken cancellationToken = default);
}
