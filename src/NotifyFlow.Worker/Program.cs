using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotifyFlow.Worker.Failures;
using NotifyFlow.Worker.Handlers;
using NotifyFlow.Worker.Messaging;
using NotifyFlow.Worker.Observability;
using NotifyFlow.Worker.Persistence;
using NotifyFlow.Worker.Providers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

builder.Services.Configure<RetryPolicyOptions>(builder.Configuration.GetSection("RetryPolicy"));
builder.Services.AddSingleton<IFailureClassifier, FailureClassifier>();

var databaseOptions = new DatabaseOptions { ConnectionString = builder.Configuration.GetConnectionString("Postgres") ?? string.Empty };
builder.Services.AddSingleton(databaseOptions);
builder.Services.AddDbContext<NotifyFlowWorkerDbContext>(options =>
    options.UseNpgsql(databaseOptions.ConnectionString));

// Scoped porque depende do NotifyFlowWorkerDbContext, que também é Scoped (E05-T03).
builder.Services.AddScoped<IProcessedEventStore, ProcessedEventStore>();

builder.Services.AddSingleton<INotificationProvider, FakeNotificationProvider>();

builder.Services.AddSingleton<INotificationHandler, UserRegisteredHandler>();
builder.Services.AddSingleton<INotificationHandler, PasswordResetRequestedHandler>();
builder.Services.AddSingleton<INotificationHandler, OrderConfirmedHandler>();

builder.Services.AddSingleton<HandlerDispatcher>();

builder.Services.AddHostedService<NotificationConsumer>();

builder.Services.AddHealthChecks()
    .AddCheck<RabbitMqHealthCheck>("rabbitmq")
    .AddCheck<PostgresHealthCheck>("postgres");

builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Period = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<IHealthCheckPublisher, LoggingHealthCheckPublisher>();

var host = builder.Build();
host.Run();
