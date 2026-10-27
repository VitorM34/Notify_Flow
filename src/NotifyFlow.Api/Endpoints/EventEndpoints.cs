using Microsoft.Extensions.Options;
using NotifyFlow.Api.Features.Events;
using NotifyFlow.Api.Features.Events.ConfirmOrder;
using NotifyFlow.Api.Features.Events.CreateEvent;
using NotifyFlow.Api.Features.Events.RegisterUser;
using NotifyFlow.Api.Features.Events.RequestPasswordReset;
using NotifyFlow.Api.Messaging;
using NotifyFlow.Api.Observability;
using NotifyFlow.Api.Validation;
using NotifyFlow.Contracts;
using NotifyFlow.Contracts.Events;

namespace NotifyFlow.Api.Endpoints;

public static class EventEndpoints
{
    // A partir da E04-T02, o endpoint não publica mais direto no RabbitMQ: ele monta o
    // EventMessage (isso não muda) e delega a persistência atômica de StoredEvent +
    // OutboxMessage para ICreateEventService. A publicação de fato fica a cargo do Outbox
    // Publisher (E04-T03), de forma assíncrona/eventual.
    //
    // A lógica de cada rota mora num método estático nomeado (Handle*Async) em vez de direto na
    // lambda do MapPost, só para ser testável unitariamente sem precisar de WebApplicationFactory
    // (E06-T02) — ver tests/NotifyFlow.Api.Tests/Endpoints/EventEndpointsTests.cs.
    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/events/user-registered", (
            RegisterUserRequest request,
            ICreateEventService createEventService,
            IOptions<RabbitMqOptions> rabbitMqOptions,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
            HandleUserRegisteredAsync(request, createEventService, rabbitMqOptions.Value.RoutingKey, logger, cancellationToken))
        .AddEndpointFilter<ValidationFilter<RegisterUserRequest>>();

        app.MapPost("/events/password-reset", (
            RequestPasswordResetRequest request,
            ICreateEventService createEventService,
            IOptions<RabbitMqOptions> rabbitMqOptions,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
            HandlePasswordResetAsync(request, createEventService, rabbitMqOptions.Value.RoutingKey, logger, cancellationToken))
        .AddEndpointFilter<ValidationFilter<RequestPasswordResetRequest>>();

        app.MapPost("/events/order-confirmed", (
            ConfirmOrderRequest request,
            ICreateEventService createEventService,
            IOptions<RabbitMqOptions> rabbitMqOptions,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
            HandleOrderConfirmedAsync(request, createEventService, rabbitMqOptions.Value.RoutingKey, logger, cancellationToken))
        .AddEndpointFilter<ValidationFilter<ConfirmOrderRequest>>();
    }

    internal static async Task<IResult> HandleUserRegisteredAsync(
        RegisterUserRequest request,
        ICreateEventService createEventService,
        string routingKey,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var message = new EventMessage
        {
            EventType = EventTypes.UserRegistered,
            Source = "notifyflow.api",
            Payload = new UserRegisteredEvent(request.UserId, request.Email, request.Name)
        };

        return await CreateAndAcceptAsync(message, createEventService, routingKey, logger, cancellationToken);
    }

    internal static async Task<IResult> HandlePasswordResetAsync(
        RequestPasswordResetRequest request,
        ICreateEventService createEventService,
        string routingKey,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var message = new EventMessage
        {
            EventType = EventTypes.PasswordResetRequested,
            Source = "notifyflow.api",
            // Nunca logar o payload inteiro aqui: PasswordResetRequestedEvent carrega o ResetToken.
            Payload = new PasswordResetRequestedEvent(request.UserId, request.Email, request.ResetToken)
        };

        return await CreateAndAcceptAsync(message, createEventService, routingKey, logger, cancellationToken);
    }

    internal static async Task<IResult> HandleOrderConfirmedAsync(
        ConfirmOrderRequest request,
        ICreateEventService createEventService,
        string routingKey,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var message = new EventMessage
        {
            EventType = EventTypes.OrderConfirmed,
            Source = "notifyflow.api",
            Payload = new OrderConfirmedEvent(request.OrderId, request.UserId, request.Email, request.Total)
        };

        return await CreateAndAcceptAsync(message, createEventService, routingKey, logger, cancellationToken);
    }

    private static async Task<IResult> CreateAndAcceptAsync(
        EventMessage message,
        ICreateEventService createEventService,
        string routingKey,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var _ = logger.BeginEventScope(message);
        logger.LogInformation(
            "Evento recebido | EventType: {EventType} | EventId: {EventId} | CorrelationId: {CorrelationId}",
            message.EventType, message.EventId, message.CorrelationId);

        var command = new CreateEventCommand(message, routingKey);
        var result = await createEventService.CreateAsync(command, cancellationToken);

        return TypedResults.Accepted("/events", result);
    }
}
