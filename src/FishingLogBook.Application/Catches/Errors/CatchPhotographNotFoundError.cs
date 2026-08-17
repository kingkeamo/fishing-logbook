using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchPhotographNotFoundError : Error
{
    public CatchPhotographNotFoundError()
        : base("The catch photograph was not found.")
    {
    }
}
