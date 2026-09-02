namespace FishingLogBook.Application.Args;

public sealed class GetCatchPhotographArgs
{
    public Guid CaughtByUserId { get; init; }

    public Guid CatchId { get; init; }

    public Guid PhotographId { get; init; }
}
