using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NotifyFlow.Api.Endpoints;
using NotifyFlow.Api.Features.Events;
using NotifyFlow.Api.Features.Events.CreateEvent;
using NotifyFlow.Api.Features.Events.RegisterUser;
using NotifyFlow.Contracts.Events;

namespace NotifyFlow.Api.Tests.Endpoints;

public class EventEndpointsTests
{
    [Fact]
    public async Task UserRegistered_endpoint_should_persist_event_and_return_accepted_response()
    {
        var expectedResponse = new EventAcceptedResponse(Guid.NewGuid(), Guid.NewGuid());
        CreateEventCommand? capturedCommand = null;

        var createEventServiceMock = new Mock<ICreateEventService>();
        createEventServiceMock
            .Setup(s => s.CreateAsync(It.IsAny<CreateEventCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreateEventCommand, CancellationToken>((command, _) => capturedCommand = command)
            .ReturnsAsync(expectedResponse);

        var request = new RegisterUserRequest(Guid.NewGuid(), "jane@example.com", "Jane");

        var result = await EventEndpoints.HandleUserRegisteredAsync(
            request,
            createEventServiceMock.Object,
            routingKey: "notifications",
            NullLogger.Instance,
            CancellationToken.None);

        var accepted = Assert.IsType<Accepted<EventAcceptedResponse>>(result);
        Assert.Equal(expectedResponse, accepted.Value);

        Assert.NotNull(capturedCommand);
        Assert.Equal(EventTypes.UserRegistered, capturedCommand!.Message.EventType);
        Assert.Equal(request.UserId, capturedCommand.Message.Payload is UserRegisteredEvent payload ? payload.UserId : Guid.Empty);
        Assert.Equal("notifications", capturedCommand.RoutingKey);
    }
}
