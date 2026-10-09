namespace NotifyFlow.Worker.Messaging;

public sealed class RabbitMqOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ExchangeName { get; set; } = string.Empty;

    public string QueueName { get; set; } = string.Empty;

    public string RoutingKey { get; set; } = string.Empty;

    public string DeadLetterExchangeName { get; set; } = string.Empty;

    public string DeadLetterQueueName { get; set; } = string.Empty;

    public ushort PrefetchCount { get; set; } = 1;
}
