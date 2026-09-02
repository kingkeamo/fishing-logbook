namespace FishingLogBook.Application.Args;

public sealed class PersistCatchLocationVisibilityArgs
{
    public Guid CatchId { get; init; }

    public Guid CaughtByUserId { get; init; }

    public string Visibility { get; init; } = string.Empty;
}
