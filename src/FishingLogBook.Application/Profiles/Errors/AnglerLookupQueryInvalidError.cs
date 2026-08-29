using FluentResults;

namespace FishingLogBook.Application.Profiles.Errors;

public sealed class AnglerLookupQueryInvalidError : Error
{
    public AnglerLookupQueryInvalidError()
        : base("The angler lookup needs a longer search term.")
    {
    }
}
