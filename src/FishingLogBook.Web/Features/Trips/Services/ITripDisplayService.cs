using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Services;

public interface ITripDisplayService
{
    Task<TripDisplayModel> DescribeAsync(TripModel trip, CancellationToken cancellationToken);

    Task<string?> DescribeInstantAsync(DateTimeOffset instant, CancellationToken cancellationToken);

    TimeSpan? Elapsed(TripModel trip);
}
