namespace NotifyFlow.Worker.Providers;

public interface INotificationProvider
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default);
}
