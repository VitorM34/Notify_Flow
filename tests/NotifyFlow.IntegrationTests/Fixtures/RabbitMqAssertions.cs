using System.Text;
using System.Text.Json;
using NotifyFlow.Contracts;
using RabbitMQ.Client;

namespace NotifyFlow.IntegrationTests.Fixtures;

/// <summary>
/// Inspeciona filas do RabbitMQ real via BasicGetAsync (não infere nada por log) — usado para
/// confirmar que uma mensagem com um EventId específico chegou a uma fila (principal, retry ou
/// DLQ). Faz polling (não um único BasicGetAsync) porque publisher confirm e visibilidade da
/// mensagem na fila não são necessariamente simultâneos do ponto de vista de quem observa por
/// outro canal.
///
/// Decisão: cada mensagem inspecionada é ACKed (removida de verdade), mesmo quando não é a
/// procurada — nack com requeue: true foi tentado antes e causa livelock (o RabbitMQ reinsere a
/// mensagem rejeitada perto do início da fila, então o polling fica preso reexaminando a mesma
/// mensagem errada pra sempre quando há mais de uma na fila). Isso é seguro aqui porque as filas
/// inspecionadas nos testes são privadas por caso (fila-sonda exclusiva) ou não são
/// reinspecionadas por nenhum outro teste (DLQ).
/// </summary>
public static class RabbitMqAssertions
{
    public static async Task<bool> QueueContainsEventAsync(
        IChannel channel,
        string queueName,
        Guid eventId,
        TimeSpan? timeout = null,
        int maxMessagesPerPoll = 50)
    {
        return await Eventually.UntilAsync(
            () => PollOnceAsync(channel, queueName, eventId, maxMessagesPerPoll),
            found => found,
            timeout ?? TimeSpan.FromSeconds(10),
            interval: TimeSpan.FromMilliseconds(200));
    }

    private static async Task<bool> PollOnceAsync(IChannel channel, string queueName, Guid eventId, int maxMessagesToInspect)
    {
        for (var i = 0; i < maxMessagesToInspect; i++)
        {
            var result = await channel.BasicGetAsync(queueName, autoAck: false);
            if (result is null)
            {
                return false;
            }

            var json = Encoding.UTF8.GetString(result.Body.Span);
            var found = TryExtractEventId(json) == eventId;

            await channel.BasicAckAsync(result.DeliveryTag, multiple: false);

            if (found)
            {
                return true;
            }
        }

        return false;
    }

    private static Guid? TryExtractEventId(string json)
    {
        try
        {
            var message = JsonSerializer.Deserialize<EventMessage>(json);
            return message?.EventId;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
