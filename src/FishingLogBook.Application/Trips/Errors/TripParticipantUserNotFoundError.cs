using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripParticipantUserNotFoundError : Error
{
    public TripParticipantUserNotFoundError()
        : base("That angler could not be found.")
    {
    }
}
