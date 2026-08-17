using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchNotOwnedError : Error
{
    public CatchNotOwnedError()
        : base("Only the catch owner may change location visibility.")
    {
    }
}
