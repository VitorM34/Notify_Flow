using Microsoft.EntityFrameworkCore;
using NotifyFlow.Api.Persistence.Entities;

namespace NotifyFlow.Api.Persistence;

public sealed class NotifyFlowApiDbContext : DbContext
{
    public NotifyFlowApiDbContext(DbContextOptions<NotifyFlowApiDbContext> options)
        : base(options)
    {
    }

    public DbSet<StoredEvent> StoredEvents => Set<StoredEvent>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Schema dedicado para a API, separado do schema "worker" — mesmo PostgreSQL,
        // mas fronteiras lógicas reforçadas entre os dois serviços (decisão da E04-T01).
        modelBuilder.HasDefaultSchema("api");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotifyFlowApiDbContext).Assembly);
    }
}
