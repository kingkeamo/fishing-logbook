using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Offline;

internal static class LocalTripAccess
{
    public static IReadOnlyList<TripModel> ForViewer(IReadOnlyList<TripModel> trips, Guid viewerUserId)
    {
        if (viewerUserId == Guid.Empty)
        {
            return [];
        }

        return [.. trips.Where(trip => trip.CanContribute(viewerUserId))];
    }

    public static IReadOnlyList<TripModel> OwnedBy(IReadOnlyList<TripModel> trips, Guid ownerUserId)
    {
        if (ownerUserId == Guid.Empty)
        {
            return [];
        }

        return [.. trips.Where(trip => trip.IsOwnedBy(ownerUserId))];
    }
}
