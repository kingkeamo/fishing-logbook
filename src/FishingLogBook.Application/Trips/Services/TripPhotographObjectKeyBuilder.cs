using FishingLogBook.Application.Trips.Contracts.Services;

namespace FishingLogBook.Application.Trips.Services;

public sealed class TripPhotographObjectKeyBuilder : ITripPhotographObjectKeyBuilder
{
    public string Build(Guid tripId, Guid photographId)
    {
        return $"trip-photographs/{tripId:D}/{photographId:D}";
    }
}
