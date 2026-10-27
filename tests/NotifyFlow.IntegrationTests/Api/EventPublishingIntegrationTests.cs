using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NotifyFlow.Api.Features.Events;
using NotifyFlow.Api.Features.Events.RegisterUser;
using NotifyFlow.Api.Persistence.Entities;
using NotifyFlow.IntegrationTests.Fixtures;

namespace NotifyFlow.IntegrationTests.Api;

[Collection(NotifyFlowEnvironmentCollection.Name)]
public class EventPublishingIntegrationTests
{
    private readonly NotifyFlowEnvironmentFixture _environment;

    public EventPublishingIntegrationTests(NotifyFlowEnvironmentFixture environment)
    {
        _environment = environment;
    }

    [Fact]
    public async Task Api_should_persist_event_and_outbox()
    {
        using var client = _environment.ApiFactory.CreateClient();
        var request = new RegisterUserRequest(Guid.NewGuid(), $"{Guid.NewGuid()}@example.com", "Integration Test");

        var response = await client.PostAsJsonAsync("/events/user-registered", request);
        response.EnsureSuccessStatusCode();

        var accepted = await response.Content.ReadFromJsonAsync<EventAcceptedResponse>();
        Assert.NotNull(accepted);

        await using var dbContext = _environment.CreateApiDbContext();

        var storedEvent = await dbContext.StoredEvents.SingleOrDefaultAsync(e => e.EventId == accepted!.EventId);
        Assert.NotNull(storedEvent);
        Assert.Equal("user.registered", storedEvent!.EventType);

        var outboxMessage = await dbContext.OutboxMessages.SingleOrDefaultAsync(m => m.EventId == accepted!.EventId);
        Assert.NotNull(outboxMessage);
        Assert.Equal(OutboxMessageStatus.Pending, outboxMessage!.Status);
        Assert.Equal("notifications", outboxMessage.RoutingKey);
    }
}
