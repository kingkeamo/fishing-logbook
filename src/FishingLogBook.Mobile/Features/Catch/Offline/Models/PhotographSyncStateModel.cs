using FishingLogBook.Mobile.Features.Catch.Offline.Enums;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Models;

public sealed class PhotographSyncStateModel
{
    public required Guid PhotographId { get; init; }

    public required Guid CatchId { get; init; }

    public required Guid UserId { get; init; }

    public required PhotographSyncStatusEnum Status { get; init; }

    public string? ObjectKey { get; init; }
}
