using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripOwnerActionRequiredError : Error
{
    public TripOwnerActionRequiredError()
        : base("Only the trip owner can do that.")
    {
    }
}
