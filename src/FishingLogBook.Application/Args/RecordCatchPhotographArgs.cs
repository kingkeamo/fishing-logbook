namespace FishingLogBook.Application.Args;

public sealed class RecordCatchPhotographArgs
{
    public Guid CatchId { get; init; }

    public Guid PhotographId { get; init; }

    public string ObjectKey { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;
}
