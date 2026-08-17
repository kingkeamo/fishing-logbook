using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchLocationInvalidError : Error
{
    public CatchLocationInvalidError()
        : base("Catch location is invalid.")
    {
    }
}
