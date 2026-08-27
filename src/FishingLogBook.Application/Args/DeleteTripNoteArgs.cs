namespace FishingLogBook.Application.Args;

public sealed class DeleteTripNoteArgs
{
    public Guid TripId { get; init; }

    public Guid NoteId { get; init; }
}
