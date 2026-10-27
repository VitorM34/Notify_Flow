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
        // Body nunca entra no log: pode conter dado sensível (ex.: ResetToken em texto puro).
        _logger.LogInformation(
            "[FAKE] Notification sent | Recipient: {Recipient} | Subject: {Subject}",
            recipient, subject);

        return Task.CompletedTask;
    }
}
