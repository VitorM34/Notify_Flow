using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyFlow.Api.Persistence.Entities;

namespace NotifyFlow.Api.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.EventType).IsRequired().HasMaxLength(200);
        builder.Property(m => m.RoutingKey).IsRequired().HasMaxLength(200);
        builder.Property(m => m.PayloadJson).IsRequired().HasColumnType("jsonb");
        builder.Property(m => m.Status).IsRequired().HasMaxLength(20);

        // Índice usado pela query de polling do Outbox Publisher (E04-T03):
        // "pegue as próximas mensagens Pending, ordenadas por CreatedAt".
        builder.HasIndex(m => new { m.Status, m.CreatedAt });
    }
}
