using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NotifyFlow.Api.Features.Events;
using NotifyFlow.Api.Features.Events.RegisterUser;
using NotifyFlow.IntegrationTests.Fixtures;
using NotifyFlow.Worker.Persistence.Entities;

namespace NotifyFlow.IntegrationTests.Worker;

[Collection(NotifyFlowEnvironmentCollection.Name)]
public class RetryAndDlqIntegrationTests : IAsyncLifetime
{
    private const string ConsumerName = "NotificationConsumer";

    private readonly NotifyFlowEnvironmentFixture _environment;
    private WorkerTestHost _workerHost = null!;

    public RetryAndDlqIntegrationTests(NotifyFlowEnvironmentFixture environment)
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
    public async Task Transient_failure_should_retry_before_succeeding()
    {
        var recipient = $"{Guid.NewGuid()}@example.com";

        // Falha transitória nas 2 primeiras tentativas (retry.1 → retry.2), sucede na 3ª.
        _workerHost.NotificationProvider.Configure(recipient, attempt =>
            attempt < 3 ? new TimeoutException("Simulando falha transitória") : null);

        using var client = _environment.ApiFactory.CreateClient();
        var request = new RegisterUserRequest(Guid.NewGuid(), recipient, "Retry Test");
        var response = await client.PostAsJsonAsync("/events/user-registered", request);
        response.EnsureSuccessStatusCode();
        var accepted = await response.Content.ReadFromJsonAsync<EventAcceptedResponse>();

        // Backoff real das retry queues (1s + 2s) + margem — ver RabbitMqRetryTopology.
        var processedEvent = await Eventually.UntilAsync(
            async () =>
            {
                await using var dbContext = _environment.CreateWorkerDbContext();
                return await dbContext.ProcessedEvents
                    .SingleOrDefaultAsync(p => p.EventId == accepted!.EventId && p.ConsumerName == ConsumerName);
            },
            (ProcessedEvent? processed) => processed is not null,
            timeout: TimeSpan.FromSeconds(20));

        Assert.NotNull(processedEvent);
        Assert.True(
            _workerHost.NotificationProvider.GetCallCount(recipient) >= 3,
            "Esperava pelo menos 3 tentativas (2 falhas + 1 sucesso) antes de processar com sucesso");
    }

    [Fact]
    public async Task Poison_message_should_end_in_dlq()
    {
        var recipient = $"{Guid.NewGuid()}@example.com";

        // Falha permanente (classificada como InvalidMessage pelo FailureClassifier) — nunca
        // tem sucesso, independente de quantas vezes for tentada, então deve ir direto pra DLQ
        // sem passar pelo ciclo de retry.
        _workerHost.NotificationProvider.Configure(recipient, _ => new InvalidOperationException("Simulando falha permanente"));

        using var client = _environment.ApiFactory.CreateClient();
        var request = new RegisterUserRequest(Guid.NewGuid(), recipient, "Poison Message Test");
        var response = await client.PostAsJsonAsync("/events/user-registered", request);
        response.EnsureSuccessStatusCode();
        var accepted = await response.Content.ReadFromJsonAsync<EventAcceptedResponse>();

        var foundInDlq = await Eventually.UntilAsync(
            async () =>
            {
                await using var channel = await _environment.CreateRabbitChannelAsync();
                return await RabbitMqAssertions.QueueContainsEventAsync(channel, "notifyflow.notifications.dlq", accepted!.EventId);
            },
            (bool found) => found,
            timeout: TimeSpan.FromSeconds(10));

        Assert.True(foundInDlq);
        Assert.Equal(1, _workerHost.NotificationProvider.GetCallCount(recipient));
    }
}
