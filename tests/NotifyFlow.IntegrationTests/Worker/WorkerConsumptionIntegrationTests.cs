using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NotifyFlow.Api.Features.Events;
using NotifyFlow.Api.Features.Events.RegisterUser;
using NotifyFlow.IntegrationTests.Fixtures;
using NotifyFlow.Worker.Persistence.Entities;

namespace NotifyFlow.IntegrationTests.Worker;

[Collection(NotifyFlowEnvironmentCollection.Name)]
public class WorkerConsumptionIntegrationTests : IAsyncLifetime
{
    private const string ConsumerName = "NotificationConsumer";

    private readonly NotifyFlowEnvironmentFixture _environment;
    private WorkerTestHost _workerHost = null!;

    public WorkerConsumptionIntegrationTests(NotifyFlowEnvironmentFixture environment)
    {
        _environment = environment;
    }

    public async Task InitializeAsync()
    {
        _workerHost = await WorkerTestHost.StartAsync(_environment.PostgresConnectionString, _environment.CreateWorkerRabbitMqOptions());
    }

    public async Task DisposeAsync()
    {
        await _workerHost.DisposeAsync();
    }

    [Fact]
    public async Task Worker_should_process_message_and_record_notification()
    {
        using var client = _environment.ApiFactory.CreateClient();
        var recipient = $"{Guid.NewGuid()}@example.com";
        var request = new RegisterUserRequest(Guid.NewGuid(), recipient, "Worker Consumption Test");

        var response = await client.PostAsJsonAsync("/events/user-registered", request);
        response.EnsureSuccessStatusCode();
        var accepted = await response.Content.ReadFromJsonAsync<EventAcceptedResponse>();

        var processedEvent = await Eventually.UntilAsync(
            async () =>
            {
                await using var dbContext = _environment.CreateWorkerDbContext();
                return await dbContext.ProcessedEvents
                    .SingleOrDefaultAsync(p => p.EventId == accepted!.EventId && p.ConsumerName == ConsumerName);
            },
            (ProcessedEvent? processed) => processed is not null);

        Assert.NotNull(processedEvent);
        Assert.Equal(1, _workerHost.NotificationProvider.GetCallCount(recipient));
    }
}
