using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyFlow.Api.Persistence.Entities;

namespace NotifyFlow.Api.Persistence.Configurations;

public sealed class StoredEventConfiguration : IEntityTypeConfiguration<StoredEvent>
{
    public void Configure(EntityTypeBuilder<StoredEvent> builder)
    {
        builder.ToTable("events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Source).IsRequired().HasMaxLength(200);
        builder.Property(e => e.PayloadJson).IsRequired().HasColumnType("jsonb");

        // EventId identifica o evento de negócio de forma única — não pode haver duas
        // linhas para o mesmo EventId (proteção contra publicação/persistência duplicada).
        builder.HasIndex(e => e.EventId).IsUnique();
    }
}
