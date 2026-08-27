using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchTripInvalidError : Error
{
    public CatchTripInvalidError()
        : base("The trip for this catch is not available.")
    {
    }
}
