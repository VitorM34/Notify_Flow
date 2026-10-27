using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NotifyFlow.Api.Features.Events;
using NotifyFlow.Api.Features.Events.RegisterUser;
using NotifyFlow.Api.Persistence.Entities;
using NotifyFlow.IntegrationTests.Fixtures;

namespace NotifyFlow.IntegrationTests.Outbox;

[Collection(NotifyFlowEnvironmentCollection.Name)]
public class OutboxPublisherIntegrationTests
{
    private readonly NotifyFlowEnvironmentFixture _environment;

    public OutboxPublisherIntegrationTests(NotifyFlowEnvironmentFixture environment)
    {
        _environment = environment;
    }

    [Fact]
    public async Task OutboxPublisher_should_publish_pending_message_to_main_queue()
    {
        // Fila exclusiva do teste, bindada na mesma exchange/routing key da fila principal —
        // recebe uma cópia independente de cada mensagem publicada (exchange Direct faz fan-out
        // por routing key), sem disputar consumo com o Worker real de outros testes da mesma
        // collection que também escutam notifyflow.notifications.
        await using var channel = await _environment.CreateRabbitChannelAsync();
        var probeQueue = await channel.QueueDeclareAsync(queue: string.Empty, durable: false, exclusive: true, autoDelete: true);
        await channel.QueueBindAsync(probeQueue.QueueName, exchange: "notifyflow.events", routingKey: "notifications");

        using var client = _environment.ApiFactory.CreateClient();
        var request = new RegisterUserRequest(Guid.NewGuid(), $"{Guid.NewGuid()}@example.com", "Outbox Test");

        var response = await client.PostAsJsonAsync("/events/user-registered", request);
        response.EnsureSuccessStatusCode();
        var accepted = await response.Content.ReadFromJsonAsync<EventAcceptedResponse>();

        // O Outbox Publisher da API (hospedado dentro do próprio WebApplicationFactory, com
        // polling a cada 1s nos testes — ver NotifyFlowApiFactory) deve pegar essa mensagem
        // Pending e publicá-la de verdade no RabbitMQ.
        var outboxMessage = await Eventually.UntilAsync(
            async () =>
            {
                await using var dbContext = _environment.CreateApiDbContext();
                return await dbContext.OutboxMessages.SingleOrDefaultAsync(m => m.EventId == accepted!.EventId);
            },
            message => message?.Status == OutboxMessageStatus.Published);

        Assert.NotNull(outboxMessage!.PublishedAt);

        var foundInQueue = await RabbitMqAssertions.QueueContainsEventAsync(channel, probeQueue.QueueName, accepted!.EventId);

        Assert.True(foundInQueue, "Mensagem publicada não chegou na fila principal (via fila-sonda bindada na mesma routing key)");
    }
}
