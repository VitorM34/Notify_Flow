using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyFlow.Worker.Persistence.Entities;

namespace NotifyFlow.Worker.Persistence.Configurations;

public sealed class NotificationRecordConfiguration : IEntityTypeConfiguration<NotificationRecord>
{
    public void Configure(EntityTypeBuilder<NotificationRecord> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.EventType).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Channel).IsRequired().HasMaxLength(50);
        builder.Property(n => n.Recipient).IsRequired().HasMaxLength(320);
        builder.Property(n => n.Status).IsRequired().HasMaxLength(20);
        builder.Property(n => n.Provider).HasMaxLength(100);

        // Consultas futuras (ex.: "todas as notificações geradas por este evento") filtram por
        // EventId — índice não-único porque um mesmo evento pode gerar múltiplos registros
        // (ex.: múltiplos destinatários).
        builder.HasIndex(n => n.EventId);
    }
}
