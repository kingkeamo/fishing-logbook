namespace FishingLogBook.Application.Args;

public sealed class AssociateTripCatchesArgs
{
    public Guid TripId { get; init; }

    public IReadOnlyList<Guid> CatchIds { get; init; } = [];
}
