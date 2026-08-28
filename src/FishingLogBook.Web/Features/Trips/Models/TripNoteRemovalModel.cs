using FishingLogBook.Web.Common;

namespace FishingLogBook.Web.Features.Trips.Models;

public sealed record TripNoteRemovalModel(
    Guid TripId,
    Guid OwnerUserId,
    Guid NoteId,
    SyncStatus SyncStatus = SyncStatus.Synchronised);
