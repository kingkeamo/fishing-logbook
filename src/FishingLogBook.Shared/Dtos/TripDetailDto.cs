using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Shared.Dtos;

public sealed record TripDetailDto(TripViewDto Trip)
{
    public IReadOnlyList<TripNoteDto> Notes { get; init; } = [];

    public IReadOnlyList<TripPhotographViewDto> Photographs { get; init; } = [];

    public IReadOnlyList<TripCatchSummaryDto> Catches { get; init; } = [];

    public string Role { get; init; } = TripParticipantConstants.None;

    public IReadOnlyList<TripContributorDto> Contributors { get; init; } = [];
}
