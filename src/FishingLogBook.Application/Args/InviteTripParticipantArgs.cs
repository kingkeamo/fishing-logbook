namespace FishingLogBook.Application.Args;

public sealed class InviteTripParticipantArgs
{
    public Guid TripId { get; init; }

    public Guid InvitedUserId { get; init; }
}
