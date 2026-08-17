namespace FishingLogBook.Application.Args;

public sealed class PersistCatchLocationVisibilityArgs
{
    public Guid CatchId { get; init; }

    public Guid UserId { get; init; }

    public string Visibility { get; init; } = string.Empty;
}
