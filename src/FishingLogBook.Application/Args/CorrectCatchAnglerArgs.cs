namespace FishingLogBook.Application.Args;

public sealed class CorrectCatchAnglerArgs
{
    public Guid CatchId { get; init; }

    public Guid AnglerUserId { get; init; }
}
