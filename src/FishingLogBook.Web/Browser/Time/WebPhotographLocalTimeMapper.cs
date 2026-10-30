using FishingLogBook.Shared.Photographs.Contracts;

namespace FishingLogBook.Web.Browser.Time;

public sealed class WebPhotographLocalTimeMapper(ITimeService time) : IPhotographLocalTimeMapper
{
    public Task<DateTimeOffset?> FromDateTimeLocalValueAsync(string localValue, CancellationToken cancellationToken)
    {
        return time.FromDateTimeLocalValueAsync(localValue, cancellationToken);
    }
}
