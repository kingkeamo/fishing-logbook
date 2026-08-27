namespace FishingLogBook.Application.Args;

public sealed class RecordTripPhotographArgs
{
    public Guid TripId { get; init; }

    public Guid PhotographId { get; init; }

    public string ObjectKey { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public DateTimeOffset AddedOn { get; init; }

    public DateTimeOffset? CapturedOn { get; init; }
}
