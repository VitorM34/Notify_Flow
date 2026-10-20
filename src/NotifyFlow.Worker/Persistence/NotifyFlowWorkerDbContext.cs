using Microsoft.EntityFrameworkCore;
using NotifyFlow.Worker.Persistence.Entities;

namespace NotifyFlow.Worker.Persistence;

public sealed class NotifyFlowWorkerDbContext : DbContext
{
    public NotifyFlowWorkerDbContext(DbContextOptions<NotifyFlowWorkerDbContext> options)
        : base(options)
    {
    }

    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();

    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Schema dedicado para o Worker, separado do schema "api" — mesmo PostgreSQL, mas
        // fronteiras lógicas reforçadas entre os dois serviços (decisão da E04-T01).
        modelBuilder.HasDefaultSchema("worker");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotifyFlowWorkerDbContext).Assembly);
    }
}
