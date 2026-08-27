namespace FishingLogBook.Shared.Dtos;

public sealed record RecordTripNoteDto(
    Guid NoteId,
    string Text,
    DateTimeOffset RecordedOn);
