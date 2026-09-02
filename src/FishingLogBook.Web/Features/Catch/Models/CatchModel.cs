using FishingLogBook.Web.Common;

namespace FishingLogBook.Web.Features.Catch.Models;

public sealed record CatchModel(
    Guid Id,
    DateTimeOffset CaughtOn,
    IReadOnlyList<CatchPhotographModel> Photographs,
    string? SpeciesName = null,
    CatchLocationModel? Location = null,
    Guid CaughtByUserId = default,
    SyncStatus SyncStatus = SyncStatus.SavedLocally,
    SyncStatus MetadataSyncStatus = SyncStatus.SavedLocally,
    Guid RecordedByUserId = default,
    decimal? Weight = null,
    decimal? Length = null,
    string? Method = null,
    string? BaitOrLure = null,
    string? Notes = null,
    DateTimeOffset? SyncedAt = null,
    Guid? TripId = null);
