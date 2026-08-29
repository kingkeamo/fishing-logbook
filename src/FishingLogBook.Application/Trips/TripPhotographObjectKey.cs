namespace FishingLogBook.Application.Trips;

internal static class TripPhotographObjectKey
{
    public static string Build(Guid tripId, Guid photographId)
    {
        return $"trip-photographs/{tripId:D}/{photographId:D}";
    }
}
