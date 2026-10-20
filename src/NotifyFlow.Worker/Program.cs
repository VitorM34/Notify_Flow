using Microsoft.EntityFrameworkCore;
using NotifyFlow.Worker.Handlers;
using NotifyFlow.Worker.Messaging;
using NotifyFlow.Worker.Persistence;
using NotifyFlow.Worker.Providers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

var databaseOptions = new DatabaseOptions { ConnectionString = builder.Configuration.GetConnectionString("Postgres") ?? string.Empty };
builder.Services.AddSingleton(databaseOptions);
builder.Services.AddDbContext<NotifyFlowWorkerDbContext>(options =>
    options.UseNpgsql(databaseOptions.ConnectionString));

builder.Services.AddSingleton<INotificationProvider, FakeNotificationProvider>();

builder.Services.AddSingleton<INotificationHandler, UserRegisteredHandler>();
builder.Services.AddSingleton<INotificationHandler, PasswordResetRequestedHandler>();
builder.Services.AddSingleton<INotificationHandler, OrderConfirmedHandler>();

builder.Services.AddSingleton<HandlerDispatcher>();

builder.Services.AddHostedService<NotificationConsumer>();

var host = builder.Build();
host.Run();
