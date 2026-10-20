namespace NotifyFlow.Api.Outbox;

/// <summary>
/// Abstrai a publicação de um lote de <c>OutboxMessage</c> pendentes (E04-T03). Separado de
/// <see cref="OutboxPublisherService"/> (o <see cref="Microsoft.Extensions.Hosting.BackgroundService"/>)
/// justamente para que o agendamento/loop de polling não se misture com a lógica de
/// ler-publicar-atualizar status.
/// </summary>
public interface IOutboxPublisher
{
    /// <summary>
    /// Lê um lote de mensagens <c>Pending</c>, publica cada uma no RabbitMQ aguardando a
    /// confirmação do broker (publisher confirm) e atualiza o status de cada mensagem de
    /// acordo com o resultado.
    /// </summary>
    Task PublishPendingAsync(CancellationToken cancellationToken = default);
}
