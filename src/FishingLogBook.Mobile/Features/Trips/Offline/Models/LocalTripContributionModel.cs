using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Offline.Models;

public sealed record LocalTripContributionModel
{
    public required Guid Id { get; init; }
    public required Guid TripId { get; init; }
    public required Guid UserId { get; init; }
    public TripNoteDto? Note { get; init; }
    public TripPhotographViewDto? Photograph { get; init; }
    public string? RelativeFilePath { get; init; }
    public string? ObjectKey { get; init; }
    public bool Deleted { get; init; }
    public long Revision { get; init; }
    public long SyncedRevision { get; init; }
    public bool IsPending => Revision > SyncedRevision;
}
