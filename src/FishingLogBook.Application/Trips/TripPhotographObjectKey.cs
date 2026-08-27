namespace FishingLogBook.Application.Trips;

internal static class TripPhotographObjectKey
{
    public static string Build(Guid userId, Guid tripId, Guid photographId)
    {
        return $"trips/{userId:D}/{tripId:D}/{photographId:D}";
    }
}
