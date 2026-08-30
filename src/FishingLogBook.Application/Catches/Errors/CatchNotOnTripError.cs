using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchNotOnTripError : Error
{
    public CatchNotOnTripError()
        : base("Only a catch attached to a trip can have its angler corrected.")
    {
    }
}
