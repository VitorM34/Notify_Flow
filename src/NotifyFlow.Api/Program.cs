using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotifyFlow.Api.Endpoints;
using NotifyFlow.Api.ErrorHandling;
using NotifyFlow.Api.Features.Events.CreateEvent;
using NotifyFlow.Api.Messaging;
using NotifyFlow.Api.Outbox;
using NotifyFlow.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var databaseOptions = new DatabaseOptions { ConnectionString = builder.Configuration.GetConnectionString("Postgres") ?? string.Empty };
builder.Services.AddSingleton(databaseOptions);
builder.Services.AddDbContext<NotifyFlowApiDbContext>(options =>
    options.UseNpgsql(databaseOptions.ConnectionString));

// Scoped porque depende do NotifyFlowApiDbContext, que também é Scoped (E04-T02).
builder.Services.AddScoped<ICreateEventService, CreateEventService>();

var rabbitMqOptions = Options.Create(builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>() ?? new RabbitMqOptions());
builder.Services.AddSingleton(rabbitMqOptions);
builder.Services.AddSingleton<IRabbitMqTopologyInitializer, RabbitMqTopologyInitializer>();

using var bootstrapLoggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
    logging.AddConsole();
});
var publisherLogger = bootstrapLoggerFactory.CreateLogger<RabbitMqPublisher>();

var rabbitMqPublisher = await RabbitMqPublisher.CreateAsync(rabbitMqOptions, publisherLogger);
builder.Services.AddSingleton<IRabbitMqPublisher>(rabbitMqPublisher);

// Outbox Publisher (E04-T03): IOutboxPublisher é Singleton (cria seu próprio escopo de DI por
// ciclo via IServiceScopeFactory para acessar o NotifyFlowApiDbContext, que é Scoped) e
// OutboxPublisherService é o BackgroundService que o chama periodicamente.
builder.Services.Configure<OutboxPublisherOptions>(builder.Configuration.GetSection("Outbox"));
builder.Services.AddSingleton<IOutboxPublisher, OutboxPublisher>();
builder.Services.AddHostedService<OutboxPublisherService>();

var app = builder.Build();

app.UseExceptionHandler();

using (var scope = app.Services.CreateScope())
{
    var topologyInitializer = scope.ServiceProvider.GetRequiredService<IRabbitMqTopologyInitializer>();
    await topologyInitializer.InitializeAsync();
}

app.MapEventEndpoints();

app.Run();
