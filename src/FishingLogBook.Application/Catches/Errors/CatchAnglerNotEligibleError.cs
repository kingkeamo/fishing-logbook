using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchAnglerNotEligibleError : Error
{
    public CatchAnglerNotEligibleError()
        : base("The selected angler is not an accepted participant of this trip.")
    {
    }
}
