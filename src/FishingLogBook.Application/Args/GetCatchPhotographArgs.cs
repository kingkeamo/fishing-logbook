namespace FishingLogBook.Application.Args;

public sealed class GetCatchPhotographArgs
{
    public Guid UserId { get; init; }

    public Guid CatchId { get; init; }

    public Guid PhotographId { get; init; }
}
