namespace NotifyFlow.Worker.Persistence.Entities;

/// <summary>
/// Valores possíveis para <see cref="NotificationRecord.Status"/>. String em vez de enum
/// para manter o mesmo padrão usado em <c>OutboxMessageStatus</c> (API): legível direto no
/// banco (psql, logs) e mais simples de evoluir sem migração de tipo.
/// </summary>
public static class NotificationStatus
{
    public const string Succeeded = "Succeeded";

    public const string Failed = "Failed";
}
