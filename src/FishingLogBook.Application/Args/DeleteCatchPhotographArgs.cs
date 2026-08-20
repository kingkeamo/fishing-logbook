namespace FishingLogBook.Application.Args;

public sealed class DeleteCatchPhotographArgs
{
    public Guid CatchId { get; init; }

    public Guid PhotographId { get; init; }
}
