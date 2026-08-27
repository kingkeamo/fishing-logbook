using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripNotFoundError : Error
{
    public TripNotFoundError()
        : base("The trip was not found.")
    {
    }
}
