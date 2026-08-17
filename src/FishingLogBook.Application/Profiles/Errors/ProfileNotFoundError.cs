using FluentResults;

namespace FishingLogBook.Application.Profiles.Errors;

public sealed class ProfileNotFoundError : Error
{
    public ProfileNotFoundError()
        : base("Angler profile was not found.")
    {
    }
}
