using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchNotFoundError : Error
{
    public CatchNotFoundError()
        : base("The catch was not found.")
    {
    }
}
