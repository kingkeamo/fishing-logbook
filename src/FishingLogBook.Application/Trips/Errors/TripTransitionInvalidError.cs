using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripTransitionInvalidError : Error
{
    public TripTransitionInvalidError()
        : base("The trip cannot change to that status.")
    {
    }
}
