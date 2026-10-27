using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NotifyFlow.Api.Features.Events;
using NotifyFlow.Api.Features.Events.RegisterUser;
using NotifyFlow.IntegrationTests.Fixtures;
using NotifyFlow.Worker.Persistence.Entities;
using RabbitMQ.Client;

namespace NotifyFlow.IntegrationTests.Worker;

[Collection(NotifyFlowEnvironmentCollection.Name)]
public class IdempotencyIntegrationTests : IAsyncLifetime
{
    private const string ConsumerName = "NotificationConsumer";

    private readonly NotifyFlowEnvironmentFixture _environment;
    private WorkerTestHost _workerHost = null!;

    public IdempotencyIntegrationTests(NotifyFlowEnvironmentFixture environment)
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
    public async Task Redelivered_message_should_not_generate_duplicate_notification()
    {
        var recipient = $"{Guid.NewGuid()}@example.com";

        using var client = _environment.ApiFactory.CreateClient();
        var request = new RegisterUserRequest(Guid.NewGuid(), recipient, "Idempotency Test");
        var response = await client.PostAsJsonAsync("/events/user-registered", request);
        response.EnsureSuccessStatusCode();
        var accepted = await response.Content.ReadFromJsonAsync<EventAcceptedResponse>();

        await Eventually.UntilAsync(
            async () =>
            {
                await using var dbContext = _environment.CreateWorkerDbContext();
                return await dbContext.ProcessedEvents
                    .SingleOrDefaultAsync(p => p.EventId == accepted!.EventId && p.ConsumerName == ConsumerName);
            },
            (ProcessedEvent? processed) => processed is not null);

        Assert.Equal(1, _workerHost.NotificationProvider.GetCallCount(recipient));

        // Simula uma redelivery: republica manualmente o mesmo payload (bytes exatos já
        // publicados pelo Outbox Publisher) direto na fila principal.
        string payloadJson;
        await using (var apiDbContext = _environment.CreateApiDbContext())
        {
            var outboxMessage = await apiDbContext.OutboxMessages.SingleAsync(m => m.EventId == accepted!.EventId);
            payloadJson = outboxMessage.PayloadJson;
        }

        await using (var channel = await _environment.CreateRabbitChannelAsync())
        {
            var properties = new BasicProperties { DeliveryMode = DeliveryModes.Persistent };
            await channel.BasicPublishAsync(
                exchange: "notifyflow.events",
                routingKey: "notifications",
                mandatory: false,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(payloadJson));
        }

        // Não há um segundo estado "verdadeiro" pra esperar (o processed_events já existe desde
        // antes) — damos uma janela curta para o Worker efetivamente receber e descartar a
        // redelivery, e então confirmamos que o provider não foi chamado de novo.
        await Task.Delay(TimeSpan.FromSeconds(3));

        Assert.Equal(1, _workerHost.NotificationProvider.GetCallCount(recipient));

        await using var finalDbContext = _environment.CreateWorkerDbContext();
        var processedCount = await finalDbContext.ProcessedEvents
            .CountAsync(p => p.EventId == accepted!.EventId && p.ConsumerName == ConsumerName);
        Assert.Equal(1, processedCount);
    }
}
