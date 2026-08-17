using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchPhotographIdentityError : Error
{
    public CatchPhotographIdentityError()
        : base("Each photograph must have its own id and belong to the catch.")
    {
    }
}
