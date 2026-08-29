using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripParticipantAlreadyRespondedError : Error
{
    public TripParticipantAlreadyRespondedError()
        : base("That trip invitation has already been answered.")
    {
    }
}
