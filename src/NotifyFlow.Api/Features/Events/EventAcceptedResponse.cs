namespace NotifyFlow.Api.Features.Events;

public sealed record EventAcceptedResponse(Guid EventId, Guid CorrelationId);
