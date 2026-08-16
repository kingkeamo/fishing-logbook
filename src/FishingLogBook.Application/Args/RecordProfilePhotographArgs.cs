namespace FishingLogBook.Application.Args;

public sealed class RecordProfilePhotographArgs
{
    public Guid UserId { get; init; }

    public Guid PhotographId { get; init; }

    public string ObjectKey { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;
}
