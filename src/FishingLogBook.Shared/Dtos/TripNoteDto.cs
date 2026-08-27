namespace FishingLogBook.Shared.Dtos;

public sealed record TripNoteDto(
    Guid Id,
    Guid TripId,
    string Text,
    DateTimeOffset RecordedOn)
{
    public Guid CreatedByUserId { get; init; }
}
