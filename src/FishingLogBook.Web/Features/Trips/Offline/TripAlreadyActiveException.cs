namespace FishingLogBook.Web.Features.Trips.Offline;

public sealed class TripAlreadyActiveException : InvalidOperationException
{
    public TripAlreadyActiveException()
        : base("An active trip already exists for this angler.")
    {
    }
}
