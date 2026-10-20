using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace NotifyFlow.Api.Persistence;

/// <summary>
/// Fábrica usada pelas ferramentas de design-time do EF Core (<c>dotnet ef migrations add</c>,
/// <c>dotnet ef database update</c>). Existe porque o <c>Program.cs</c> da API faz uma chamada de
/// rede bloqueante (<c>RabbitMqPublisher.CreateAsync</c>) antes de <c>builder.Build()</c> — se a
/// ferramenta do EF tentasse montar o host executando o <c>Program.cs</c> normalmente, ela
/// travaria/falharia sempre que o RabbitMQ não estivesse de pé. Implementando
/// <see cref="IDesignTimeDbContextFactory{TContext}"/>, o EF Core usa esta fábrica diretamente e
/// nunca chega a executar o <c>Program.cs</c>.
/// </summary>
public sealed class NotifyFlowApiDbContextFactory : IDesignTimeDbContextFactory<NotifyFlowApiDbContext>
{
    public NotifyFlowApiDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Postgres") ?? string.Empty;

        var optionsBuilder = new DbContextOptionsBuilder<NotifyFlowApiDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new NotifyFlowApiDbContext(optionsBuilder.Options);
    }
}
