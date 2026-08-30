using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripParticipantNotFoundError : Error
{
    public TripParticipantNotFoundError()
        : base("The trip participant was not found.")
    {
    }
}
