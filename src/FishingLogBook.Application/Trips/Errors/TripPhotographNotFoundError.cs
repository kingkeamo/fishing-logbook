using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripPhotographNotFoundError : Error
{
    public TripPhotographNotFoundError()
        : base("The trip photograph could not be found.")
    {
    }
}
