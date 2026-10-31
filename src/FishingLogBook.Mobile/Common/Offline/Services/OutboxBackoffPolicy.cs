namespace FishingLogBook.Mobile.Common.Offline.Services;

public sealed class OutboxBackoffPolicy
{
    public static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan DefaultMaximumDelay = TimeSpan.FromMinutes(15);
    public const int DefaultMaximumAttempts = 8;
    private const double JitterRatio = 0.2;

    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maximumDelay;
    private readonly Func<double> _nextRandom;

    public OutboxBackoffPolicy()
        : this(DefaultBaseDelay, DefaultMaximumDelay, DefaultMaximumAttempts, Random.Shared.NextDouble)
    {
    }

    public OutboxBackoffPolicy(
        TimeSpan baseDelay,
        TimeSpan maximumDelay,
        int maximumAttempts,
        Func<double> nextRandom)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(baseDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDelay, baseDelay);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumAttempts, 1);
        ArgumentNullException.ThrowIfNull(nextRandom);
        _baseDelay = baseDelay;
        _maximumDelay = maximumDelay;
        MaximumAttempts = maximumAttempts;
        _nextRandom = nextRandom;
    }

    public int MaximumAttempts { get; }

    public bool IsExhausted(int attemptCount)
    {
        return attemptCount >= MaximumAttempts;
    }

    public TimeSpan NextDelay(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 30);
        var uncapped = _baseDelay.TotalSeconds * Math.Pow(2, exponent);
        var capped = Math.Min(uncapped, _maximumDelay.TotalSeconds);
        var jitter = 1 + ((Math.Clamp(_nextRandom(), 0d, 1d) * 2) - 1) * JitterRatio;
        return TimeSpan.FromSeconds(Math.Min(capped * jitter, _maximumDelay.TotalSeconds));
    }
}
