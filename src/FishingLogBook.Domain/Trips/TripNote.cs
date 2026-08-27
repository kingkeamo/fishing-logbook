namespace FishingLogBook.Domain.Trips;

public sealed class TripNote
{
    public Guid Id { get; init; }

    public Guid TripId { get; init; }

    public string Text { get; init; } = string.Empty;

    public DateTimeOffset RecordedOn { get; init; }
}
