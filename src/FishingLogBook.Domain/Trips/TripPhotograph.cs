namespace FishingLogBook.Domain.Trips;

public sealed class TripPhotograph
{
    public Guid Id { get; init; }

    public Guid TripId { get; init; }

    public string ObjectKey { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public DateTimeOffset? CapturedOn { get; init; }

    public DateTimeOffset AddedOn { get; init; }

    public DateTimeOffset OrderedOn
    {
        get
        {
            return CapturedOn ?? AddedOn;
        }
    }
}
