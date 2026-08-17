namespace FishingLogBook.Application.Args;

public sealed class UpdateCatchLocationVisibilityArgs
{
    public Guid CatchId { get; init; }

    public string Visibility { get; init; } = string.Empty;
}
