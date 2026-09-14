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
            var message = new EventMessage
            {
                EventType = EventTypes.UserRegistered,
                Source = "notifyflow.api",
                Payload = new UserRegisteredEvent(request.UserId, request.Email, request.Name)
            };

            await publisher.PublishAsync(message, cancellationToken);

            return Results.Accepted("/events", new { message.EventId, message.CorrelationId });
        });

        app.MapPost("/events/password-reset", async (
            PasswordResetRequest request,
            IRabbitMqPublisher publisher,
            CancellationToken cancellationToken) =>
        {
            var message = new EventMessage
            {
                EventType = EventTypes.PasswordResetRequested,
                Source = "notifyflow.api",
                Payload = new PasswordResetRequestedEvent(request.UserId, request.Email, request.ResetToken)
            };

            await publisher.PublishAsync(message, cancellationToken);

            return Results.Accepted("/events", new { message.EventId, message.CorrelationId });
        });

        app.MapPost("/events/order-confirmed", async (
            OrderConfirmedRequest request,
            IRabbitMqPublisher publisher,
            CancellationToken cancellationToken) =>
        {
            var message = new EventMessage
            {
                EventType = EventTypes.OrderConfirmed,
                Source = "notifyflow.api",
                Payload = new OrderConfirmedEvent(request.OrderId, request.UserId, request.Email, request.Total)
            };

            await publisher.PublishAsync(message, cancellationToken);

            return Results.Accepted("/events", new { message.EventId, message.CorrelationId });
        });
    }
}

public sealed record UserRegisteredRequest(Guid UserId, string Email, string Name);
public sealed record PasswordResetRequest(Guid UserId, string Email, string ResetToken);
public sealed record OrderConfirmedRequest(Guid OrderId, Guid UserId, string Email, decimal Total);
