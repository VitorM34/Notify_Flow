using NotifyFlow.Api.Features.Events;
using NotifyFlow.Api.Features.Events.ConfirmOrder;
using NotifyFlow.Api.Features.Events.RegisterUser;
using NotifyFlow.Api.Features.Events.RequestPasswordReset;
using NotifyFlow.Api.Messaging;
using NotifyFlow.Api.Validation;
using NotifyFlow.Contracts;
using NotifyFlow.Contracts.Events;

namespace NotifyFlow.Api.Endpoints;

public static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/events/user-registered", async (
            RegisterUserRequest request,
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

            return TypedResults.Accepted("/events", new EventAcceptedResponse(message.EventId, message.CorrelationId));
        })
        .AddEndpointFilter<ValidationFilter<RegisterUserRequest>>();

        app.MapPost("/events/password-reset", async (
            RequestPasswordResetRequest request,
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

            return TypedResults.Accepted("/events", new EventAcceptedResponse(message.EventId, message.CorrelationId));
        })
        .AddEndpointFilter<ValidationFilter<RequestPasswordResetRequest>>();

        app.MapPost("/events/order-confirmed", async (
            ConfirmOrderRequest request,
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

            return TypedResults.Accepted("/events", new EventAcceptedResponse(message.EventId, message.CorrelationId));
        })
        .AddEndpointFilter<ValidationFilter<ConfirmOrderRequest>>();
    }
}
