using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripParticipantAlreadyInvitedError : Error
{
    public TripParticipantAlreadyInvitedError()
        : base("That angler has already been invited to this trip.")
    {
    }
}
