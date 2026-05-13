namespace TheSexy6BotWorker.Services;

public interface IMcpJitterProvider
{
    double Next();
}

public interface IMcpReconnectDelayPolicy
{
    TimeSpan GetDelay(int attempt);
}

public interface IMcpDelayScheduler
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class RandomMcpJitterProvider : IMcpJitterProvider
{
    public double Next() => Random.Shared.NextDouble();
}

public sealed class ExponentialMcpReconnectDelayPolicy(IMcpJitterProvider jitterProvider) : IMcpReconnectDelayPolicy
{
    private const double BaseDelaySeconds = 2;
    private const double MaximumDelaySeconds = 60;
    private const double JitterCeiling = 0.20;

    public TimeSpan GetDelay(int attempt)
    {
        if (attempt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt), "Attempt must be greater than zero.");
        }

        var exponential = BaseDelaySeconds * Math.Pow(2, attempt - 1);
        var bounded = Math.Min(exponential, MaximumDelaySeconds);
        var jitterMultiplier = 1d + (Math.Clamp(jitterProvider.Next(), 0d, 1d) * JitterCeiling);
        var jittered = Math.Min(bounded * jitterMultiplier, MaximumDelaySeconds);
        var milliseconds = Math.Round(jittered * 1000, MidpointRounding.AwayFromZero);

        return TimeSpan.FromMilliseconds(milliseconds);
    }
}

public sealed class SystemMcpDelayScheduler : IMcpDelayScheduler
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
