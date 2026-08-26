using FishingLogBook.Web.Common;

namespace FishingLogBook.Web.Features.Trips.Models;

public sealed record TripModel(
    Guid Id,
    Guid OwnerUserId,
    string Status,
    DateTimeOffset StartedOn,
    DateTimeOffset? EndedOn = null,
    string? Title = null,
    string? PlaceName = null,
    TripLocationModel? Location = null,
    SyncStatus SyncStatus = SyncStatus.SavedLocally,
    DateTimeOffset? SyncedAt = null);
