namespace NotifyFlow.Api.Persistence.Entities;

/// <summary>
/// Valores possíveis de <see cref="OutboxMessage.Status"/>.
/// Representado como string (em vez de enum) para manter o mesmo estilo de
/// "nomes centralizados em constantes" já usado em NotifyFlow.Contracts.Events.EventTypes,
/// e para evitar o custo de conversão de enum no E04-T03 (filtragem por status via SQL cru/migrations).
/// </summary>
public static class OutboxMessageStatus
{
    public const string Pending = "Pending";
    public const string Published = "Published";
    public const string Failed = "Failed";
}
