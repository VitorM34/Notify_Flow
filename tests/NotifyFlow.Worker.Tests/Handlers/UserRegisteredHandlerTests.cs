using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NotifyFlow.Contracts;
using NotifyFlow.Contracts.Events;
using NotifyFlow.Worker.Handlers;
using NotifyFlow.Worker.Providers;

namespace NotifyFlow.Worker.Tests.Handlers;

public class UserRegisteredHandlerTests
{
    [Fact]
    public async Task UserRegisteredHandler_should_build_welcome_message()
    {
        var providerMock = new Mock<INotificationProvider>();
        var handler = new UserRegisteredHandler(providerMock.Object, NullLogger<UserRegisteredHandler>.Instance);

        var payload = new UserRegisteredEvent(Guid.NewGuid(), "jane@example.com", "Jane");
        var message = new EventMessage
        {
            EventType = EventTypes.UserRegistered,
            Source = "tests",
            // Payload precisa ser um JsonElement de verdade aqui: em produção, é isso que o
            // handler recebe depois que o Worker desserializa o EventMessage vindo do RabbitMQ
            // (EventMessage.Payload é `object`, mas o runtime type pós-JSON é sempre JsonElement).
            Payload = JsonSerializer.SerializeToElement(payload)
        };

        await handler.HandleAsync(message);

        providerMock.Verify(p => p.SendAsync(
            "jane@example.com",
            "Welcome to NotifyFlow",
            It.Is<string>(body => body.Contains("Jane")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
