using FishingLogBook.Web.Common;

namespace FishingLogBook.Web.Features.Catch.Models;

public sealed record CatchModel(
    Guid Id,
    DateTimeOffset CaughtOn,
    IReadOnlyList<CatchPhotographModel> Photographs,
    string? SpeciesName = null,
    CatchLocationModel? Location = null,
    Guid UserId = default,
    SyncStatus SyncStatus = SyncStatus.SavedLocally,
    SyncStatus MetadataSyncStatus = SyncStatus.SavedLocally,
    Guid AnglerUserId = default,
    Guid RecordedByUserId = default);
