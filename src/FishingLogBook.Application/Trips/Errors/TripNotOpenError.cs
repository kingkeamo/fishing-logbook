using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripNotOpenError : Error
{
    public TripNotOpenError()
        : base("The trip has not started or was cancelled.")
    {
    }
}
