using FluentResults;

namespace FishingLogBook.Application.FishingLocations.Errors;

public sealed class DuplicateFishingLocationError : Error
{
    public DuplicateFishingLocationError(string message)
        : base(message)
    {
    }
}
