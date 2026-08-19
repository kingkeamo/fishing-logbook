using FluentResults;

namespace FishingLogBook.Application.FishingPreferences.Errors;

public sealed class UnknownFishingCatalogueEntryError : Error
{
    public UnknownFishingCatalogueEntryError(string message)
        : base(message)
    {
    }
}
