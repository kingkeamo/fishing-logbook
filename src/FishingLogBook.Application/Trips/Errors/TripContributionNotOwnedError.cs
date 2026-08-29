using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripContributionNotOwnedError : Error
{
    public TripContributionNotOwnedError()
        : base("Only the angler who added it can change it.")
    {
    }
}
