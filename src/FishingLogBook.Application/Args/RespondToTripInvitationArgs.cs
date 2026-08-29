using FishingLogBook.Domain.Enums;

namespace FishingLogBook.Application.Args;

public sealed class RespondToTripInvitationArgs
{
    public Guid TripId { get; init; }

    public TripParticipantStatusEnum Response { get; init; }
}
