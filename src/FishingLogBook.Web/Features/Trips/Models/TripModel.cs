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
    DateTimeOffset? SyncedAt = null,
    IReadOnlyList<TripPhotographModel>? Photographs = null,
    IReadOnlyList<TripNoteModel>? Notes = null)
{
    public IReadOnlyList<TripPhotographModel> Photographs { get; init; } =
        Photographs is { Count: > 0 } ? Photographs : [];

    public IReadOnlyList<TripNoteModel> Notes { get; init; } =
        Notes is { Count: > 0 } ? Notes : [];
}
