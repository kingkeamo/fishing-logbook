using FluentResults;

namespace FishingLogBook.Application.Profiles.Errors;

public sealed class PhotographObjectKeyMismatchError : Error
{
    public PhotographObjectKeyMismatchError()
        : base("Photograph object key does not match the profile.")
    {
    }
}
