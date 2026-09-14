using NotifyFlow.Api.Endpoints;
using NotifyFlow.Api.ErrorHandling;
using NotifyFlow.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

using var bootstrapLoggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
    logging.AddConsole();
});
var publisherLogger = bootstrapLoggerFactory.CreateLogger<RabbitMqPublisher>();

var rabbitMqPublisher = await RabbitMqPublisher.CreateAsync(builder.Configuration, publisherLogger);
builder.Services.AddSingleton<IRabbitMqPublisher>(rabbitMqPublisher);

var app = builder.Build();

app.UseExceptionHandler();

app.MapEventEndpoints();

app.Run();
