using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripPhotographObjectKeyMismatchError : Error
{
    public TripPhotographObjectKeyMismatchError()
        : base("The trip photograph object key does not match the trip.")
    {
    }
}
