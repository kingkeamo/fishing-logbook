using FluentResults;

namespace FishingLogBook.Application.Profiles.Errors;

public sealed class ObjectStorageNotConfiguredError : Error
{
    public ObjectStorageNotConfiguredError()
        : base("Object storage is not configured.")
    {
    }
}
