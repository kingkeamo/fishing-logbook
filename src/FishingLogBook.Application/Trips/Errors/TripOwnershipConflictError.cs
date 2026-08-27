using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripOwnershipConflictError : Error
{
    public TripOwnershipConflictError()
        : base("Trip ownership cannot be changed.")
    {
    }
}
