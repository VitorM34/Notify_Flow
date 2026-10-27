namespace NotifyFlow.IntegrationTests.Fixtures;

public static class Eventually
{
    public static async Task<T> UntilAsync<T>(
        Func<Task<T?>> probe,
        Func<T?, bool> isSatisfied,
        TimeSpan? timeout = null,
        TimeSpan? interval = null)
    {
        timeout ??= TimeSpan.FromSeconds(15);
        interval ??= TimeSpan.FromMilliseconds(250);
        var deadline = DateTime.UtcNow + timeout.Value;

        T? last = default;

        while (DateTime.UtcNow < deadline)
        {
            last = await probe();

            if (isSatisfied(last))
            {
                return last!;
            }

            await Task.Delay(interval.Value);
        }

        throw new TimeoutException(
            $"Condição não satisfeita após {timeout.Value.TotalSeconds}s. Último valor observado: {last}");
    }
}
