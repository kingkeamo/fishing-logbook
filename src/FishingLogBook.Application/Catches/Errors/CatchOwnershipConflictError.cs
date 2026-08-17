using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchOwnershipConflictError : Error
{
    public CatchOwnershipConflictError()
        : base("Catch ownership cannot be changed.")
    {
    }
}
