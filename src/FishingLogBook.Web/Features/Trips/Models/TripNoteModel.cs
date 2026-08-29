using FishingLogBook.Web.Common;

namespace FishingLogBook.Web.Features.Trips.Models;

public sealed record TripNoteModel(
    Guid Id,
    Guid TripId,
    Guid CreatedByUserId,
    string Text,
    DateTimeOffset RecordedOn,
    SyncStatus SyncStatus = SyncStatus.SavedLocally,
    DateTimeOffset? SyncedAt = null);
