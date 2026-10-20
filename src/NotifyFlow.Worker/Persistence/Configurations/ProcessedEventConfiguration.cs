using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyFlow.Worker.Persistence.Entities;

namespace NotifyFlow.Worker.Persistence.Configurations;

public sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("processed_events");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.ConsumerName).IsRequired().HasMaxLength(200);

        // Garante idempotência: o mesmo consumidor não pode processar o mesmo evento duas
        // vezes (base da E05-T03).
        builder.HasIndex(p => new { p.EventId, p.ConsumerName }).IsUnique();
    }
}
