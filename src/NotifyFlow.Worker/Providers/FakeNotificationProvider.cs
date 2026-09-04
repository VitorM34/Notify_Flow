using Microsoft.Extensions.Logging;

namespace NotifyFlow.Worker.Providers;

public sealed class FakeNotificationProvider : INotificationProvider
{
    private readonly ILogger<FakeNotificationProvider> _logger;

    public FakeNotificationProvider(ILogger<FakeNotificationProvider> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[FAKE] Notification sent | Recipient: {Recipient} | Subject: {Subject} | Body: {Body}",
            recipient, subject, body);

        return Task.CompletedTask;
    }
}
