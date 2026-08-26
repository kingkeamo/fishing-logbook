using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripLocationInvalidError : Error
{
    public TripLocationInvalidError()
        : base("The trip location is not valid.")
    {
    }
}
