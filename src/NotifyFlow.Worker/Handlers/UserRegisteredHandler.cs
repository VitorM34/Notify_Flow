using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotifyFlow.Contracts;
using NotifyFlow.Contracts.Events;
using NotifyFlow.Worker.Providers;

namespace NotifyFlow.Worker.Handlers;

public sealed class UserRegisteredHandler : INotificationHandler
{
    private readonly INotificationProvider _provider;
    private readonly ILogger<UserRegisteredHandler> _logger;

    public string EventType => "user.registered";

    public UserRegisteredHandler(INotificationProvider provider, ILogger<UserRegisteredHandler> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<UserRegisteredEvent>(
            JsonSerializer.Serialize(envelope.Payload))
            ?? throw new InvalidOperationException($"Invalid payload for {EventType}");

        _logger.LogInformation(
            "Handling {EventType} | EventId: {EventId} | UserId: {UserId}",
            EventType, envelope.EventId, payload.UserId);

        await _provider.SendAsync(
            recipient: payload.Email,
            subject: "Welcome to NotifyFlow",
            body: $"Hello, {payload.Name}! Welcome aboard.",
            cancellationToken: cancellationToken);
    }
}
