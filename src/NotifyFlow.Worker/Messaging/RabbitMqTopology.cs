namespace NotifyFlow.Worker.Messaging;

public sealed class RabbitMqTopology
{
    public RabbitMqTopology(RabbitMqOptions options)
    {
        ExchangeName = options.ExchangeName;
        QueueName = options.QueueName;
        RoutingKey = options.RoutingKey;
        DeadLetterExchangeName = options.DeadLetterExchangeName;
        DeadLetterQueueName = options.DeadLetterQueueName;
    }

    public string ExchangeName { get; }

    public string QueueName { get; }

    public string RoutingKey { get; }

    public string DeadLetterExchangeName { get; }

    public string DeadLetterQueueName { get; }
}
