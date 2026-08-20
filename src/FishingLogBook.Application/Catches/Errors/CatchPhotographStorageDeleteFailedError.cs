using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchPhotographStorageDeleteFailedError : Error
{
    public CatchPhotographStorageDeleteFailedError()
        : base("Failed to delete the catch photograph from object storage.")
    {
    }
}
