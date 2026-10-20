namespace NotifyFlow.Worker.Messaging;

public static class RabbitMqRetryTopology
{
    public const string RetryExchangeName = "notifyflow.notifications.retry";

    public const string RetryQueueName1 = "notifyflow.notifications.retry.1";
    public const string RetryQueueName2 = "notifyflow.notifications.retry.2";
    public const string RetryQueueName3 = "notifyflow.notifications.retry.3";

    public const string RetryRoutingKey1 = "notifications.retry.1";
    public const string RetryRoutingKey2 = "notifications.retry.2";
    public const string RetryRoutingKey3 = "notifications.retry.3";

    public const int RetryTtlMs1 = 1000;
    public const int RetryTtlMs2 = 2000;
    public const int RetryTtlMs3 = 4000;
}
