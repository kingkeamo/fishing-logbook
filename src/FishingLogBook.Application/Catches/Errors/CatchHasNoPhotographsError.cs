using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchHasNoPhotographsError : Error
{
    public CatchHasNoPhotographsError()
        : base("A catch requires at least one photograph.")
    {
    }
}
