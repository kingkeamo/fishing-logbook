using System.Globalization;
using FishingLogBook.Shared.Photographs.Contracts;

namespace FishingLogBook.Mobile.Features.Photographs.Services;

public sealed class DevicePhotographLocalTimeMapper(TimeProvider timeProvider) : IPhotographLocalTimeMapper
{
    private const string DateTimeLocalFormat = "yyyy-MM-ddTHH:mm:ss";

    public Task<DateTimeOffset?> FromDateTimeLocalValueAsync(string localValue, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!DateTime.TryParseExact(
                localValue,
                DateTimeLocalFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var wallClock))
        {
            return Task.FromResult<DateTimeOffset?>(null);
        }

        var offset = timeProvider.LocalTimeZone.GetUtcOffset(wallClock);
        return Task.FromResult<DateTimeOffset?>(new DateTimeOffset(wallClock, offset));
    }
}
