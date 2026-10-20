using Microsoft.Extensions.Options;
using NotifyFlow.Api.Features.Events.ConfirmOrder;
using NotifyFlow.Api.Features.Events.CreateEvent;
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
        // A partir da E04-T02, o endpoint não publica mais direto no RabbitMQ: ele monta o
        // EventMessage (isso não muda) e delega a persistência atômica de StoredEvent +
        // OutboxMessage para ICreateEventService. A publicação de fato fica a cargo do Outbox
        // Publisher (E04-T03), de forma assíncrona/eventual.
        app.MapPost("/events/user-registered", async (
            RegisterUserRequest request,
            ICreateEventService createEventService,
            IOptions<RabbitMqOptions> rabbitMqOptions,
            CancellationToken cancellationToken) =>
        {
            var message = new EventMessage
            {
                EventType = EventTypes.UserRegistered,
                Source = "notifyflow.api",
                Payload = new UserRegisteredEvent(request.UserId, request.Email, request.Name)
            };

            var command = new CreateEventCommand(message, rabbitMqOptions.Value.RoutingKey);
            var result = await createEventService.CreateAsync(command, cancellationToken);

            return TypedResults.Accepted("/events", result);
        })
        .AddEndpointFilter<ValidationFilter<RegisterUserRequest>>();

        app.MapPost("/events/password-reset", async (
            RequestPasswordResetRequest request,
            ICreateEventService createEventService,
            IOptions<RabbitMqOptions> rabbitMqOptions,
            CancellationToken cancellationToken) =>
        {
            var message = new EventMessage
            {
                EventType = EventTypes.PasswordResetRequested,
                Source = "notifyflow.api",
                Payload = new PasswordResetRequestedEvent(request.UserId, request.Email, request.ResetToken)
            };

            var command = new CreateEventCommand(message, rabbitMqOptions.Value.RoutingKey);
            var result = await createEventService.CreateAsync(command, cancellationToken);

            return TypedResults.Accepted("/events", result);
        })
        .AddEndpointFilter<ValidationFilter<RequestPasswordResetRequest>>();

        app.MapPost("/events/order-confirmed", async (
            ConfirmOrderRequest request,
            ICreateEventService createEventService,
            IOptions<RabbitMqOptions> rabbitMqOptions,
            CancellationToken cancellationToken) =>
        {
            var message = new EventMessage
            {
                EventType = EventTypes.OrderConfirmed,
                Source = "notifyflow.api",
                Payload = new OrderConfirmedEvent(request.OrderId, request.UserId, request.Email, request.Total)
            };

            var command = new CreateEventCommand(message, rabbitMqOptions.Value.RoutingKey);
            var result = await createEventService.CreateAsync(command, cancellationToken);

            return TypedResults.Accepted("/events", result);
        })
        .AddEndpointFilter<ValidationFilter<ConfirmOrderRequest>>();
    }
}
