using Microsoft.Extensions.Options;
using NotifyFlow.Api.Endpoints;
using NotifyFlow.Api.ErrorHandling;
using NotifyFlow.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

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

var app = builder.Build();

app.UseExceptionHandler();

using (var scope = app.Services.CreateScope())
{
    var topologyInitializer = scope.ServiceProvider.GetRequiredService<IRabbitMqTopologyInitializer>();
    await topologyInitializer.InitializeAsync();
}

app.MapEventEndpoints();

app.Run();
