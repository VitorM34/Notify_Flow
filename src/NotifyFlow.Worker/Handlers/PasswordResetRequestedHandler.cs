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

    public string EventType => "password.reset.requested";

    public PasswordResetRequestedHandler(INotificationProvider provider, ILogger<PasswordResetRequestedHandler> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<PasswordResetRequestedEvent>(
            JsonSerializer.Serialize(envelope.Payload))
            ?? throw new InvalidOperationException($"Invalid payload for {EventType}");

        _logger.LogInformation(
            "Handling {EventType} | EventId: {EventId} | UserId: {UserId}",
            EventType, envelope.EventId, payload.UserId);

        await _provider.SendAsync(
            recipient: payload.Email,
            subject: "Password Reset",
            body: $"Your reset token is: {payload.ResetToken}",
            cancellationToken: cancellationToken);
    }
}
