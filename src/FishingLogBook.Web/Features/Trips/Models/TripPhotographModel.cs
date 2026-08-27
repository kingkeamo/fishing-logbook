using FishingLogBook.Web.Common;

namespace FishingLogBook.Web.Features.Trips.Models;

public sealed record TripPhotographModel(
    Guid Id,
    Guid TripId,
    Guid OwnerUserId,
    string ContentType,
    DateTimeOffset AddedOn,
    DateTimeOffset? CapturedOn = null,
    byte[]? Bytes = null,
    string? ObjectKey = null,
    SyncStatus SyncStatus = SyncStatus.SavedLocally,
    DateTimeOffset? SyncedAt = null)
{
    public DateTimeOffset OrderedOn
    {
        get
        {
            return CapturedOn ?? AddedOn;
        }
    }
}
