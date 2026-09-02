namespace FishingLogBook.Application.Args;

public sealed class PersistCatchTripArgs
{
    public Guid CatchId { get; init; }

    public Guid CaughtByUserId { get; init; }

    public Guid TripId { get; init; }
}
