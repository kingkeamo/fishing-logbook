namespace FishingLogBook.Application.Args;

public sealed class PersistCatchAnglerArgs
{
    public Guid CatchId { get; init; }

    public Guid AnglerUserId { get; init; }
}
