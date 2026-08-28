namespace FishingLogBook.Application.Args;

public sealed class PersistCatchTripArgs
{
    public Guid CatchId { get; init; }

    public Guid UserId { get; init; }

    public Guid TripId { get; init; }
}
