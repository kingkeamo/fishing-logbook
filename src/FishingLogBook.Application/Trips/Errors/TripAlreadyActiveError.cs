using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripAlreadyActiveError : Error
{
    public TripAlreadyActiveError()
        : base("An active trip already exists.")
    {
    }
}
