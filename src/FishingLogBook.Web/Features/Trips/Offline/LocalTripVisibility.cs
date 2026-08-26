using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Offline;

internal static class LocalTripVisibility
{
    public static IReadOnlyList<TripModel> ForOwner(
        IReadOnlyList<TripModel> trips,
        Guid ownerUserId)
    {
        if (ownerUserId == Guid.Empty)
        {
            return [];
        }

        return trips
            .Where(trip => trip.OwnerUserId == ownerUserId)
            .ToArray();
    }
}
