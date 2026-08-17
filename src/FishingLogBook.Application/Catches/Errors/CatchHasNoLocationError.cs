using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchHasNoLocationError : Error
{
    public CatchHasNoLocationError()
        : base("Location visibility cannot be changed when the catch has no location.")
    {
    }
}
