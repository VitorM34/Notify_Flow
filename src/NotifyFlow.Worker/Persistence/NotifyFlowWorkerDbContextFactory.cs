using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace NotifyFlow.Worker.Persistence;

/// <summary>
/// Fábrica usada pelas ferramentas de design-time do EF Core (<c>dotnet ef migrations add</c>,
/// <c>dotnet ef database update</c>) para o projeto Worker. Mesmo motivo do equivalente na API
/// (ver <c>NotifyFlowApiDbContextFactory</c>): evita que o EF Core precise executar o
/// <c>Program.cs</c> real (e suas dependências de infraestrutura) só para descobrir o
/// <see cref="NotifyFlowWorkerDbContext"/>.
/// </summary>
public sealed class NotifyFlowWorkerDbContextFactory : IDesignTimeDbContextFactory<NotifyFlowWorkerDbContext>
{
    public NotifyFlowWorkerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Postgres") ?? string.Empty;

        var optionsBuilder = new DbContextOptionsBuilder<NotifyFlowWorkerDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new NotifyFlowWorkerDbContext(optionsBuilder.Options);
    }
}
