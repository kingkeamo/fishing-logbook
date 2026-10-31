namespace FishingLogBook.Mobile.Features.Catch.Offline.Models;

public sealed class LocalCatchCleanupCandidateModel
{
    public required Guid CatchId { get; init; }

    public required Guid UserId { get; init; }

    public required long Revision { get; init; }

    public required IReadOnlyList<Guid> PhotographIds { get; init; }

    public required IReadOnlyList<string> RelativeFilePaths { get; init; }
}
