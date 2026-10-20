using NotifyFlow.Contracts;

namespace NotifyFlow.Api.Features.Events.CreateEvent;

/// <summary>
/// Contrato interno entre o endpoint HTTP e <see cref="ICreateEventService"/> — não é o Request
/// DTO (validação HTTP) nem o <see cref="EventMessage"/> em si, embora carregue um. Existe para
/// que o serviço de aplicação saiba, além dos dados do evento, para qual routing key a mensagem
/// de outbox correspondente deve ser endereçada quando for publicada (E04-T03).
/// </summary>
public sealed record CreateEventCommand(EventMessage Message, string RoutingKey);
