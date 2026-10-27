using Microsoft.Extensions.Logging.Abstractions;
using NotifyFlow.Contracts;
using NotifyFlow.Worker.Handlers;

namespace NotifyFlow.Worker.Tests.Handlers;

public class HandlerDispatcherTests
{
    private sealed class FakeHandler : INotificationHandler
    {
        public FakeHandler(string eventType) => EventType = eventType;

        public string EventType { get; }

        public int CallCount { get; private set; }

        public EventMessage? ReceivedMessage { get; private set; }

        public Task HandleAsync(EventMessage message, CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedMessage = message;
            return Task.CompletedTask;
        }
    }

    private static EventMessage CreateMessage(string eventType) => new()
    {
        EventType = eventType,
        Source = "tests",
        Payload = new { }
    };

    [Fact]
    public async Task Dispatcher_should_resolve_handler_by_event_type()
    {
        var matchingHandler = new FakeHandler("user.registered");
        var otherHandler = new FakeHandler("order.confirmed");
        var dispatcher = new HandlerDispatcher(
            new INotificationHandler[] { matchingHandler, otherHandler },
            NullLogger<HandlerDispatcher>.Instance);
        var message = CreateMessage("user.registered");

        await dispatcher.DispatchAsync(message);

        Assert.Equal(1, matchingHandler.CallCount);
        Assert.Same(message, matchingHandler.ReceivedMessage);
        Assert.Equal(0, otherHandler.CallCount);
    }

    [Fact]
    public async Task Dispatcher_should_handle_unknown_event_type()
    {
        var handler = new FakeHandler("user.registered");
        var dispatcher = new HandlerDispatcher(
            new INotificationHandler[] { handler },
            NullLogger<HandlerDispatcher>.Instance);
        var message = CreateMessage("unknown.event.type");

        // Comportamento documentado (HandlerDispatcher.cs): tipo desconhecido só loga warning e
        // retorna, sem lançar exceção nem chamar handler nenhum.
        var exception = await Record.ExceptionAsync(() => dispatcher.DispatchAsync(message));

        Assert.Null(exception);
        Assert.Equal(0, handler.CallCount);
    }
}
