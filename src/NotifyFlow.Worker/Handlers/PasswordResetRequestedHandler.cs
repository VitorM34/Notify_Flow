using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotifyFlow.Contracts;
using NotifyFlow.Contracts.Events;
using NotifyFlow.Worker.Providers;

namespace NotifyFlow.Worker.Handlers;

public sealed class PasswordResetRequestedHandler : INotificationHandler
{
    private readonly INotificationProvider _provider;
    private readonly ILogger<PasswordResetRequestedHandler> _logger;

    public string EventType => EventTypes.PasswordResetRequested;

    public PasswordResetRequestedHandler(INotificationProvider provider, ILogger<PasswordResetRequestedHandler> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task HandleAsync(EventMessage message, CancellationToken cancellationToken = default)
    {
        var payload = ((JsonElement)message.Payload).Deserialize<PasswordResetRequestedEvent>()
            ?? throw new InvalidOperationException($"Invalid payload for {EventType}");

        _logger.LogInformation(
            "Handling {EventType} | EventId: {EventId} | CorrelationId: {CorrelationId} | UserId: {UserId}",
            EventType, message.EventId, message.CorrelationId, payload.UserId);

        await _provider.SendAsync(
            recipient: payload.Email,
            subject: "Password Reset",
            body: $"Your reset token is: {payload.ResetToken}",
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Notificação enviada | EventType: {EventType} | EventId: {EventId}",
            EventType, message.EventId);
    }
}
