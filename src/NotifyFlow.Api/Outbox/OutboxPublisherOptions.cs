namespace NotifyFlow.Api.Outbox;

/// <summary>
/// Configuração do Outbox Publisher (E04-T03), vinculada à seção <c>Outbox</c> do
/// <c>appsettings.json</c> via <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>,
/// no mesmo padrão já usado por <see cref="NotifyFlow.Api.Messaging.RabbitMqOptions"/>.
/// </summary>
public sealed class OutboxPublisherOptions
{
    /// <summary>Intervalo, em segundos, entre dois ciclos de polling da outbox.</summary>
    public int IntervalSeconds { get; set; } = 5;

    /// <summary>Quantidade máxima de mensagens <c>Pending</c> processadas por ciclo.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// Número máximo de tentativas de publicação antes de desistir de uma mensagem e marcá-la
    /// como <see cref="Persistence.Entities.OutboxMessageStatus.Failed"/> em vez de deixá-la
    /// indefinidamente <c>Pending</c> para novas tentativas. Decisão arquitetural (E04-T03,
    /// documentada aqui e em OutboxPublisher): sem esse limite, uma mensagem permanentemente
    /// não publicável (ex.: exchange removida) seria tentada para sempre, a cada ciclo, sem
    /// nunca sinalizar operacionalmente que precisa de intervenção manual.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;
}
