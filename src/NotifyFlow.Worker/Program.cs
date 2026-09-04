using NotifyFlow.Worker.Handlers;
using NotifyFlow.Worker.Messaging;
using NotifyFlow.Worker.Providers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<INotificationProvider, FakeNotificationProvider>();

builder.Services.AddSingleton<INotificationHandler, UserRegisteredHandler>();
builder.Services.AddSingleton<INotificationHandler, PasswordResetRequestedHandler>();
builder.Services.AddSingleton<INotificationHandler, OrderConfirmedHandler>();

builder.Services.AddSingleton<HandlerDispatcher>();

builder.Services.AddHostedService<NotificationConsumer>();

var host = builder.Build();
host.Run();
