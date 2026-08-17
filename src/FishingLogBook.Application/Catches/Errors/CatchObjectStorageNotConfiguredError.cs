using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchObjectStorageNotConfiguredError : Error
{
    public CatchObjectStorageNotConfiguredError()
        : base("Object storage is not configured.")
    {
    }
}
