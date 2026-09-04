using NotifyFlow.Api.Endpoints;
using NotifyFlow.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IRabbitMqPublisher>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return RabbitMqPublisher.CreateAsync(config).GetAwaiter().GetResult();
});

var app = builder.Build();

app.MapEventEndpoints();

app.Run();
