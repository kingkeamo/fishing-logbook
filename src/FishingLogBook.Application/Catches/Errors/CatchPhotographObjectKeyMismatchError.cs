using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchPhotographObjectKeyMismatchError : Error
{
    public CatchPhotographObjectKeyMismatchError()
        : base("The photograph object key does not match the catch.")
    {
    }
}
