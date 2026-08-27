using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripObjectStorageNotConfiguredError : Error
{
    public TripObjectStorageNotConfiguredError()
        : base("Photograph storage is not configured.")
    {
    }
}
