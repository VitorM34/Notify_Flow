namespace NotifyFlow.Worker.Failures;

public sealed class RetryPolicyOptions
{
    public int MaxAttempts { get; set; } = 3;

    public int InitialDelayMs { get; set; } = 1000;

    public double Multiplier { get; set; } = 2;

    public int MaxDelayMs { get; set; } = 4000;
}
