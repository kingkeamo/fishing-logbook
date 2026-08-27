namespace FishingLogBook.Application.Args;

public sealed class DeleteTripPhotographArgs
{
    public Guid TripId { get; init; }

    public Guid PhotographId { get; init; }
}
