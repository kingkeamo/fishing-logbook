namespace FishingLogBook.Application.Args;

public sealed class RemoveTripParticipantArgs
{
    public Guid TripId { get; init; }

    public Guid ParticipantUserId { get; init; }
}
