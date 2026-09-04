using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotifyFlow.Contracts;
using NotifyFlow.Contracts.Events;
using NotifyFlow.Worker.Providers;

namespace NotifyFlow.Worker.Handlers;

public sealed class OrderConfirmedHandler : INotificationHandler
{
    private readonly INotificationProvider _provider;
    private readonly ILogger<OrderConfirmedHandler> _logger;

    public string EventType => "order.confirmed";

    public OrderConfirmedHandler(INotificationProvider provider, ILogger<OrderConfirmedHandler> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<OrderConfirmedEvent>(
            JsonSerializer.Serialize(envelope.Payload))
            ?? throw new InvalidOperationException($"Invalid payload for {EventType}");

        _logger.LogInformation(
            "Handling {EventType} | EventId: {EventId} | OrderId: {OrderId}",
            EventType, envelope.EventId, payload.OrderId);

        await _provider.SendAsync(
            recipient: payload.Email,
            subject: "Order Confirmed",
            body: $"Your order {payload.OrderId} was confirmed. Total: {payload.Total:C}",
            cancellationToken: cancellationToken);
    }
}
