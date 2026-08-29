namespace FishingLogBook.Shared.Dtos;

public sealed record TripParticipantsDto(Guid TripId, string Role)
{
    public IReadOnlyList<TripParticipantDto> Participants { get; init; } = [];
}
