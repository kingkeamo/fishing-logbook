using FluentResults;

namespace FishingLogBook.Application.Capabilities.Errors;

public sealed class CurrentUserUnresolvedError : Error
{
    public CurrentUserUnresolvedError()
        : base("The current user is not resolved.")
    {
    }
}
