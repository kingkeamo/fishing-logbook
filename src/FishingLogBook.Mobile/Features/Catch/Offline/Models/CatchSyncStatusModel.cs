using FishingLogBook.Mobile.Features.Catch.Offline.Enums;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Models;

public sealed class CatchSyncStatusModel
{
    public required Guid CatchId { get; init; }

    public required CatchSyncStateEnum State { get; init; }

    public required bool MetadataSynced { get; init; }

    public required int PhotographCount { get; init; }

    public required int PhotographsRecorded { get; init; }

    public required bool CanRetry { get; init; }

    public DateTimeOffset? NextAttemptAt { get; init; }
}
