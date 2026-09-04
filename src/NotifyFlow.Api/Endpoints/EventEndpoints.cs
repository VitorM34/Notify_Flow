using NotifyFlow.Api.Messaging;
using NotifyFlow.Contracts;
using NotifyFlow.Contracts.Events;

namespace NotifyFlow.Api.Endpoints;

public static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/events/user-registered", async (
            UserRegisteredRequest request,
            IRabbitMqPublisher publisher,
            CancellationToken cancellationToken) =>
        {
            var envelope = new EventEnvelope
            {
                EventType = "user.registered",
                Source = "notifyflow.api",
                Payload = new UserRegisteredEvent(request.UserId, request.Email, request.Name)
            };

            await publisher.PublishAsync(envelope, cancellationToken);

            return Results.Accepted("/events", new { envelope.EventId, envelope.CorrelationId });
        });

        app.MapPost("/events/password-reset", async (
            PasswordResetRequest request,
            IRabbitMqPublisher publisher,
            CancellationToken cancellationToken) =>
        {
            var envelope = new EventEnvelope
            {
                EventType = "password.reset.requested",
                Source = "notifyflow.api",
                Payload = new PasswordResetRequestedEvent(request.UserId, request.Email, request.ResetToken)
            };

            await publisher.PublishAsync(envelope, cancellationToken);

            return Results.Accepted("/events", new { envelope.EventId, envelope.CorrelationId });
        });

        app.MapPost("/events/order-confirmed", async (
            OrderConfirmedRequest request,
            IRabbitMqPublisher publisher,
            CancellationToken cancellationToken) =>
        {
            var envelope = new EventEnvelope
            {
                EventType = "order.confirmed",
                Source = "notifyflow.api",
                Payload = new OrderConfirmedEvent(request.OrderId, request.UserId, request.Email, request.Total)
            };

            await publisher.PublishAsync(envelope, cancellationToken);

            return Results.Accepted("/events", new { envelope.EventId, envelope.CorrelationId });
        });
    }
}

public sealed record UserRegisteredRequest(Guid UserId, string Email, string Name);
public sealed record PasswordResetRequest(Guid UserId, string Email, string ResetToken);
public sealed record OrderConfirmedRequest(Guid OrderId, Guid UserId, string Email, decimal Total);
