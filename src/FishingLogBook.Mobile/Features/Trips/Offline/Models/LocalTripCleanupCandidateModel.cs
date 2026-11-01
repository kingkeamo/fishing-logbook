namespace FishingLogBook.Mobile.Features.Trips.Offline.Models;

public sealed class LocalTripCleanupCandidateModel
{
    public required Guid TripId { get; init; }

    public required Guid UserId { get; init; }

    public required long Revision { get; init; }

    public required IReadOnlyList<string> RelativeFilePaths { get; init; }
}
