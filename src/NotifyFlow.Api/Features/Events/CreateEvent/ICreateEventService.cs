namespace NotifyFlow.Api.Features.Events.CreateEvent;

/// <summary>
/// Caso de uso de recebimento de evento na API: persiste o <c>StoredEvent</c> e a
/// <c>OutboxMessage</c> correspondente de forma atômica, substituindo a publicação direta no
/// RabbitMQ que existia antes da E04-T02 (Transactional Outbox).
/// </summary>
public interface ICreateEventService
{
    Task<EventAcceptedResponse> CreateAsync(CreateEventCommand command, CancellationToken cancellationToken = default);
}
