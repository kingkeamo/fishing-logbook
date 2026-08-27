using FishingLogBook.Web.Features.Trips.Enums;

namespace FishingLogBook.Web.Features.Trips.Models;

public sealed record TripTimelineItemModel(
    TripTimelineKindEnum Kind,
    DateTimeOffset OccurredOn)
{
    public Guid? CatchId { get; init; }

    public string? SpeciesName { get; init; }

    public string? Text { get; init; }

    public string? PhotographUrl { get; init; }

    public int PhotographCount { get; init; }
}
