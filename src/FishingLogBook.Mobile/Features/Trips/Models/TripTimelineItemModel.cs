using FishingLogBook.Mobile.Features.Trips.Enums;

namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record TripTimelineItemModel(TripTimelineKindEnum Kind, DateTimeOffset OccurredOn)
{
    public Guid? CatchId { get; init; }
    public Guid? NoteId { get; init; }
    public Guid? PhotographId { get; init; }
    public Guid ContributedByUserId { get; init; }
    public Guid RecordedByUserId { get; init; }
    public string? Text { get; init; }
    public string? SpeciesName { get; init; }
    public decimal? Weight { get; init; }
    public decimal? Length { get; init; }
    public string? PhotographUrl { get; init; }
    public string? LocalFilePath { get; init; }
    public bool IsPending { get; init; }

    public Guid Identity => CatchId ?? NoteId ?? PhotographId ?? Guid.Empty;
}
