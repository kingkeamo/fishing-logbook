using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripParticipantSelfInviteError : Error
{
    public TripParticipantSelfInviteError()
        : base("A trip owner cannot invite themselves.")
    {
    }
}
