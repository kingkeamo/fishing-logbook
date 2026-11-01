using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Offline.Models;

public sealed record LocalTripRecordModel
{
    public required Guid UserId { get; init; }
    public required TripDetailDto Detail { get; init; }
    public long Revision { get; init; }
    public long SyncedRevision { get; init; }
    public bool IsServerOrigin { get; init; }
    public bool AccessRevoked { get; init; }
    public bool HasPendingMetadata => Revision > SyncedRevision;
    public bool IsActive => Detail.Trip.Status == TripConstants.Active;
    public bool IsPlanned => Detail.Trip.Status == TripConstants.Planned;
    public bool IsCancelled => Detail.Trip.Status == TripConstants.Cancelled;
    public bool HasPlannedLifecycle => IsPlanned || IsCancelled;
    public bool HasAccess => !AccessRevoked && Detail.Role is TripParticipantConstants.Owner or TripParticipantConstants.Participant;
    public bool CanContribute => HasAccess && !HasPlannedLifecycle;
    public bool CanManage => HasAccess && Detail.Trip.OwnerUserId == UserId;

    public TripNoteDto? FindNote(Guid noteId)
    {
        return Detail.Notes.FirstOrDefault(note => note.Id == noteId);
    }
}
