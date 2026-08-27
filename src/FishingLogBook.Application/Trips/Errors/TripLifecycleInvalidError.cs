using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripLifecycleInvalidError : Error
{
    public TripLifecycleInvalidError()
        : base("The trip start and end times are not valid.")
    {
    }
}
