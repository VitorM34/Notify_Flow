using System.Collections.Concurrent;
using NotifyFlow.Worker.Providers;

namespace NotifyFlow.IntegrationTests.Fixtures;

/// <summary>
/// Substitui o FakeNotificationProvider de produção nos testes de integração (E06-T03, Passo 10):
/// permite configurar, por destinatário, se a chamada deve suceder, lançar uma falha transitória
/// (TimeoutException) ou uma falha permanente/inválida. Chaveado por destinatário porque cada
/// teste usa um e-mail único (GUID), então não há necessidade de reset entre casos.
/// </summary>
public sealed class ScenarioNotificationProvider : INotificationProvider
{
    private readonly ConcurrentDictionary<string, int> _callCounts = new();
    private readonly ConcurrentDictionary<string, Func<int, Exception?>> _scenarios = new();

    public int GetCallCount(string recipient) => _callCounts.GetValueOrDefault(recipient);

    /// <summary>
    /// Registra o comportamento para um destinatário: a função recebe o número da tentativa
    /// (1-based) e devolve a exceção a lançar, ou null para suceder.
    /// </summary>
    public void Configure(string recipient, Func<int, Exception?> scenario)
    {
        _scenarios[recipient] = scenario;
    }

    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        var attempt = _callCounts.AddOrUpdate(recipient, 1, (_, count) => count + 1);

        if (_scenarios.TryGetValue(recipient, out var scenario))
        {
            var exception = scenario(attempt);
            if (exception is not null)
            {
                throw exception;
            }
        }

        return Task.CompletedTask;
    }
}
