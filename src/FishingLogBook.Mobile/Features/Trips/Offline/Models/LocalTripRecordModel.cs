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
    public bool CanContribute => !AccessRevoked && Detail.Role is TripParticipantConstants.Owner or TripParticipantConstants.Participant;
    public bool CanManage => CanContribute && Detail.Trip.OwnerUserId == UserId;
}
