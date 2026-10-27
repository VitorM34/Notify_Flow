using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NotifyFlow.Worker.Failures;
using NotifyFlow.Worker.Handlers;
using NotifyFlow.Worker.Messaging;
using NotifyFlow.Worker.Persistence;
using NotifyFlow.Worker.Providers;

namespace NotifyFlow.IntegrationTests.Fixtures;

/// <summary>
/// Hospeda o Worker de verdade (NotificationConsumer real, handlers reais, IFailureClassifier e
/// IProcessedEventStore reais contra Postgres real) dentro do processo de teste, usando os
/// mesmos registros de DI de src/NotifyFlow.Worker/Program.cs — só troca INotificationProvider
/// pelo ScenarioNotificationProvider (ponto de substituição intencional: produção já usa um
/// FakeNotificationProvider como placeholder de um provider real futuro).
/// </summary>
public sealed class WorkerTestHost : IAsyncDisposable
{
    private readonly IHost _host;

    private WorkerTestHost(IHost host, ScenarioNotificationProvider notificationProvider)
    {
        _host = host;
        NotificationProvider = notificationProvider;
    }

    public ScenarioNotificationProvider NotificationProvider { get; }

    public static async Task<WorkerTestHost> StartAsync(string postgresConnectionString, RabbitMqOptions rabbitMqOptions)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton(Options.Create(rabbitMqOptions));
        builder.Services.AddSingleton(Options.Create(new RetryPolicyOptions { MaxAttempts = 3 }));
        builder.Services.AddSingleton<IFailureClassifier, FailureClassifier>();

        builder.Services.AddDbContext<NotifyFlowWorkerDbContext>(options =>
            options.UseNpgsql(postgresConnectionString));
        builder.Services.AddScoped<IProcessedEventStore, ProcessedEventStore>();

        var notificationProvider = new ScenarioNotificationProvider();
        builder.Services.AddSingleton<INotificationProvider>(notificationProvider);

        builder.Services.AddSingleton<INotificationHandler, UserRegisteredHandler>();
        builder.Services.AddSingleton<INotificationHandler, PasswordResetRequestedHandler>();
        builder.Services.AddSingleton<INotificationHandler, OrderConfirmedHandler>();
        builder.Services.AddSingleton<HandlerDispatcher>();

        builder.Services.AddHostedService<NotificationConsumer>();

        var host = builder.Build();
        await host.StartAsync();

        return new WorkerTestHost(host, notificationProvider);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}
