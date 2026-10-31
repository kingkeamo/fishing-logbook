namespace FishingLogBook.Mobile.Tests.Support;

public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;
    private readonly TimeZoneInfo _localTimeZone;

    public FixedTimeProvider(DateTimeOffset now, TimeZoneInfo? localTimeZone = null)
    {
        _now = now;
        _localTimeZone = localTimeZone ?? TimeZoneInfo.Utc;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }

    public override TimeZoneInfo LocalTimeZone => _localTimeZone;
}
