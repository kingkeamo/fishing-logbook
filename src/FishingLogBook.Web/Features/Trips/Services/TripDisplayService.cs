using System.Globalization;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Services;

public sealed class TripDisplayService : ITripDisplayService
{
    private readonly ITimeService _timeService;

    public TripDisplayService(ITimeService timeService)
    {
        _timeService = timeService;
    }

    public async Task<TripDisplayModel> DescribeAsync(TripModel trip, CancellationToken cancellationToken)
    {
        var startedLocal = await ToLocalAsync(trip.StartedOn, cancellationToken);
        var endedLocal = trip.EndedOn is null
            ? null
            : await ToLocalAsync(trip.EndedOn.Value, cancellationToken);
        return new TripDisplayModel(
            startedLocal?.ToString("d MMM yyyy", CultureInfo.CurrentCulture),
            startedLocal?.ToString("t", CultureInfo.CurrentCulture),
            endedLocal?.ToString("t", CultureInfo.CurrentCulture),
            Elapsed(trip));
    }

    public TimeSpan? Elapsed(TripModel trip)
    {
        var until = trip.EndedOn ?? DateTimeOffset.UtcNow;
        var elapsed = until - trip.StartedOn;
        return elapsed < TimeSpan.Zero ? null : elapsed;
    }

    private async Task<DateTime?> ToLocalAsync(DateTimeOffset instant, CancellationToken cancellationToken)
    {
        var value = await _timeService.ToDateTimeLocalValueAsync(instant, cancellationToken);
        return DateTime.TryParseExact(
            value,
            "yyyy-MM-ddTHH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }
}
