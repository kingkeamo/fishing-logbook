using FishingLogBook.Application.Trips.Contracts.Builders;

namespace FishingLogBook.Application.Trips.Builders;

public sealed class TripPhotographObjectKeyBuilder : ITripPhotographObjectKeyBuilder
{
    public string Build(Guid tripId, Guid photographId)
    {
        return $"trip-photographs/{tripId:D}/{photographId:D}";
    }
}
