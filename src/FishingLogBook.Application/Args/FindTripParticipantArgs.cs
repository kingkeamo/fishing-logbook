namespace FishingLogBook.Application.Args;

public sealed class FindTripParticipantArgs
{
    public Guid TripId { get; init; }

    public Guid UserId { get; init; }
}
