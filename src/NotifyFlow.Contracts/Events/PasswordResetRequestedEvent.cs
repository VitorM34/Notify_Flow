namespace NotifyFlow.Contracts.Events;

public sealed record PasswordResetRequestedEvent(Guid UserId, string Email, string ResetToken);
