namespace FishingLogBook.Application.Args;

public sealed class RecordTripNoteArgs
{
    public Guid TripId { get; init; }

    public Guid NoteId { get; init; }

    public string Text { get; init; } = string.Empty;

    public DateTimeOffset RecordedOn { get; init; }
}
