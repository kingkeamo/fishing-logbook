using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchDetailsInvalidError : Error
{
    public CatchDetailsInvalidError()
        : base("Catch details are invalid.")
    {
    }
}
