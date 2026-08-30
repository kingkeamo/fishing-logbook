using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Enums;

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
    DateTimeOffset? SyncedAt = null,
    IReadOnlyList<TripPhotographModel>? Photographs = null,
    IReadOnlyList<TripNoteModel>? Notes = null,
    IReadOnlyList<Guid>? ParticipantUserIds = null,
    TripOriginEnum Origin = TripOriginEnum.Local)
{
    public IReadOnlyList<TripPhotographModel> Photographs { get; init; } =
        Photographs is { Count: > 0 } ? Photographs : [];

    public IReadOnlyList<TripNoteModel> Notes { get; init; } =
        Notes is { Count: > 0 } ? Notes : [];

    public IReadOnlyList<Guid> ParticipantUserIds { get; init; } =
        ParticipantUserIds is { Count: > 0 } ? ParticipantUserIds : [];

    public bool IsOwnedBy(Guid userId)
    {
        return userId != Guid.Empty && OwnerUserId == userId;
    }

    public bool CanContribute(Guid userId)
    {
        return IsOwnedBy(userId) || (userId != Guid.Empty && ParticipantUserIds.Contains(userId));
    }

    public TripAccessRoleEnum RoleFor(Guid userId)
    {
        if (IsOwnedBy(userId))
        {
            return TripAccessRoleEnum.Owner;
        }

        return CanContribute(userId) ? TripAccessRoleEnum.Participant : TripAccessRoleEnum.None;
    }
}
