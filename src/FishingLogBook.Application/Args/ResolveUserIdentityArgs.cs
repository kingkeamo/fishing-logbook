namespace FishingLogBook.Application.Args;

public sealed class ResolveUserIdentityArgs
{
    public string Provider { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;
}
