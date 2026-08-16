namespace FishingLogBook.Application.Args;

public sealed class FindUserIdentityArgs
{
    public string Provider { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;
}
