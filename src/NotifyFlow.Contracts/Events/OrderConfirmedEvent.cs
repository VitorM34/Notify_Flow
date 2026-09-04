namespace NotifyFlow.Contracts.Events;

public sealed record OrderConfirmedEvent(Guid OrderId, Guid UserId, string Email, decimal Total);
