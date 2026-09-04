namespace NotifyFlow.Contracts.Events;

public sealed record UserRegisteredEvent(Guid UserId, string Email, string Name);
