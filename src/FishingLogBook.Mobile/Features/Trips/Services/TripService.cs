using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Stores;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public sealed class TripService(ITripStore store, ITripClient client, IAccountSessionCoordinator session,
    IConnectivityService connectivity, TimeProvider time) : ITripService
{
    public async Task<TripHistoryModel> LoadAsync(CancellationToken cancellationToken)
    {
        var lease = RequireLease();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.CancellationToken);
        var local = await store.ListAsync(lease.UserId, linked.Token);
        var response = connectivity.IsOnline ? await client.ListAsync(linked.Token) : null;
        EnsureCurrent(lease);
        var merged = response?.Value?.ToDictionary(trip => trip.Id) ?? [];
        var pendingIds = new HashSet<Guid>();
        foreach (var trip in local)
        {
            var contributions = await store.ListContributionsAsync(lease.UserId, trip.Detail.Trip.Id, linked.Token);
            if (trip.IsServerOrigin && response?.IsSuccess == true && !merged.ContainsKey(trip.Detail.Trip.Id))
            {
                await store.RevokeAsync(lease, trip.Detail.Trip.Id, linked.Token);
                continue;
            }
            var pending = trip.HasPendingMetadata || contributions.Any(item => item.IsPending);
            if (pending)
            {
                pendingIds.Add(trip.Detail.Trip.Id);
            }
            if (!trip.AccessRevoked && (pending || trip.IsActive))
            {
                if (trip.HasPendingMetadata || !merged.ContainsKey(trip.Detail.Trip.Id))
                {
                    merged[trip.Detail.Trip.Id] = ToSummary(trip);
                }
            }
        }
        EnsureCurrent(lease);
        return new TripHistoryModel(merged.Values.OrderByDescending(trip => trip.Status == TripConstants.Active)
            .ThenByDescending(trip => trip.StartedOn).ThenBy(trip => trip.Id).ToArray(), pendingIds, response?.IsSuccess != true);
    }

    public async Task<LocalTripRecordModel?> GetAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var lease = RequireLease();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.CancellationToken);
        var local = await store.GetAsync(lease.UserId, tripId, linked.Token);
        var response = connectivity.IsOnline ? await client.GetAsync(tripId, linked.Token) : null;
        EnsureCurrent(lease);
        if (response?.Outcome is ApiOutcomeEnum.NotFound or ApiOutcomeEnum.Forbidden)
        {
            if (local is null || local.IsServerOrigin || local.SyncedRevision > 0)
            {
                await store.RevokeAsync(lease, tripId, linked.Token);
                return null;
            }
        }
        if (response?.IsSuccess == true)
        {
            var remote = new LocalTripRecordModel { UserId = lease.UserId, Detail = response.Value!, IsServerOrigin = true };
            if (remote.IsActive || local is not null)
            {
                await store.HydrateAsync(lease, remote, linked.Token);
            }
            local = local?.HasPendingMetadata == true
                ? local with { Detail = remote.Detail with { Trip = local.Detail.Trip } }
                : remote;
        }
        if (local is null || local.AccessRevoked)
        {
            return null;
        }
        var contributions = await store.ListContributionsAsync(lease.UserId, tripId, linked.Token);
        EnsureCurrent(lease);
        return Overlay(local, contributions);
    }

    public async Task<LocalTripRecordModel> StartAsync(string? title, string? placeName, CancellationToken cancellationToken)
    {
        var lease = RequireLease();
        var existing = await store.ListAsync(lease.UserId, cancellationToken);
        if (existing.Any(trip => trip.IsActive && trip.CanManage))
        {
            throw new InvalidOperationException("An owned trip is already active.");
        }
        var now = time.GetUtcNow();
        var trip = new LocalTripRecordModel
        {
            UserId = lease.UserId,
            Detail = new TripDetailDto(new TripViewDto(Guid.NewGuid(), lease.UserId, TripConstants.Active, now)
            {
                Title = TripConstants.TrimTitle(title),
                PlaceName = TripConstants.TrimPlaceName(placeName)
            })
            { Role = TripParticipantConstants.Owner }
        };
        await store.SaveAsync(lease, trip, 0, cancellationToken);
        return trip with { Revision = 1 };
    }

    public async Task<LocalTripRecordModel> UpdateAsync(LocalTripRecordModel trip, TripViewDto edited, CancellationToken cancellationToken)
    {
        var lease = RequireLease();
        RequireContributor(lease, trip);
        if (!trip.CanManage || !trip.IsActive || edited.Id != trip.Detail.Trip.Id || edited.OwnerUserId != lease.UserId)
        {
            throw new UnauthorizedAccessException();
        }
        ValidateLifecycle(edited);
        var saved = trip with
        {
            Detail = trip.Detail with
            {
                Trip = edited with
                {
                    Title = TripConstants.TrimTitle(edited.Title),
                    PlaceName = TripConstants.TrimPlaceName(edited.PlaceName)
                }
            }
        };
        await store.HydrateAsync(lease, trip, cancellationToken);
        var current = await store.GetAsync(lease.UserId, edited.Id, cancellationToken);
        await store.SaveAsync(lease, saved, current!.Revision, cancellationToken);
        return saved with { Revision = current.Revision + 1, SyncedRevision = current.SyncedRevision };
    }

    public async Task SaveNoteAsync(LocalTripRecordModel trip, TripNoteDto note, bool deleted, CancellationToken cancellationToken)
    {
        var lease = RequireLease();
        RequireContributor(lease, trip);
        if (note.CreatedByUserId != lease.UserId || note.TripId != trip.Detail.Trip.Id)
        {
            throw new UnauthorizedAccessException();
        }
        if (!deleted && (!TripConstants.IsNoteTextValid(note.Text) || note.RecordedOn < trip.Detail.Trip.StartedOn
            || note.RecordedOn > (trip.Detail.Trip.EndedOn ?? time.GetUtcNow())))
        {
            throw new ArgumentException("Invalid trip note.", nameof(note));
        }
        await SaveContributionAsync(lease, trip, new LocalTripContributionModel
        {
            Id = note.Id,
            TripId = note.TripId,
            UserId = lease.UserId,
            Note = note with { Text = note.Text.Trim() },
            Deleted = deleted
        }, cancellationToken);
    }

    public async Task SavePhotographAsync(LocalTripRecordModel trip, TripPhotographViewDto photograph, string? relativeFilePath, bool deleted, CancellationToken cancellationToken)
    {
        var lease = RequireLease();
        RequireContributor(lease, trip);
        if (photograph.ContributedByUserId != lease.UserId || (!deleted && !trip.IsActive))
        {
            throw new UnauthorizedAccessException();
        }
        if (!deleted && (relativeFilePath is null || trip.Detail.Photographs.Count >= 10))
        {
            throw new ArgumentException("Invalid trip photograph.", nameof(photograph));
        }
        await SaveContributionAsync(lease, trip, new LocalTripContributionModel
        {
            Id = photograph.Id,
            TripId = trip.Detail.Trip.Id,
            UserId = lease.UserId,
            Photograph = photograph,
            RelativeFilePath = relativeFilePath,
            Deleted = deleted
        }, cancellationToken);
    }

    private async Task SaveContributionAsync(AccountSessionLeaseModel lease, LocalTripRecordModel trip, LocalTripContributionModel contribution, CancellationToken cancellationToken)
    {
        await store.HydrateAsync(lease, trip, cancellationToken);
        var existing = (await store.ListContributionsAsync(lease.UserId, contribution.TripId, cancellationToken))
            .SingleOrDefault(item => item.Id == contribution.Id);
        await store.SaveContributionAsync(lease, contribution with
        {
            RelativeFilePath = contribution.RelativeFilePath ?? existing?.RelativeFilePath
        }, existing?.Revision ?? 0, cancellationToken);
    }

    private static LocalTripRecordModel Overlay(LocalTripRecordModel trip, IReadOnlyList<LocalTripContributionModel> contributions)
    {
        var notes = trip.Detail.Notes.ToDictionary(note => note.Id);
        var photos = trip.Detail.Photographs.ToDictionary(photo => photo.Id);
        foreach (var item in contributions)
        {
            if (item.Note is { } note)
            {
                if (item.Deleted) { notes.Remove(item.Id); }
                else if (item.IsPending || !notes.ContainsKey(item.Id)) { notes[item.Id] = note; }
            }
            if (item.Photograph is { } photo)
            {
                if (item.Deleted) { photos.Remove(item.Id); }
                else if (item.IsPending || !photos.ContainsKey(item.Id)) { photos[item.Id] = photo; }
            }
        }
        return trip with
        {
            Detail = trip.Detail with
            {
                Notes = notes.Values.OrderBy(note => note.RecordedOn).ThenBy(note => note.Id).ToArray(),
                Photographs = photos.Values.OrderBy(photo => photo.CapturedOn ?? photo.AddedOn).ThenBy(photo => photo.Id).ToArray()
            }
        };
    }

    private void ValidateLifecycle(TripViewDto trip)
    {
        var now = time.GetUtcNow();
        if (!TripConstants.IsKnownStatus(trip.Status) || !TripConstants.IsStartedOnValid(trip.StartedOn, now)
            || !TripConstants.IsEndedOnValid(trip.StartedOn, trip.EndedOn, now)
            || (trip.Status == TripConstants.Active && trip.EndedOn is not null))
        {
            throw new ArgumentException("Invalid trip lifecycle.", nameof(trip));
        }
    }

    private static void RequireContributor(AccountSessionLeaseModel lease, LocalTripRecordModel trip)
    {
        if (trip.UserId != lease.UserId || !trip.CanContribute)
        {
            throw new UnauthorizedAccessException();
        }
    }

    private AccountSessionLeaseModel RequireLease()
    {
        return session.Capture() ?? throw new UnauthorizedAccessException();
    }

    private void EnsureCurrent(AccountSessionLeaseModel lease)
    {
        if (!session.IsCurrent(lease))
        {
            throw new OperationCanceledException(lease.CancellationToken);
        }
    }

    private static TripSummaryDto ToSummary(LocalTripRecordModel trip)
    {
        var detail = trip.Detail;
        return new TripSummaryDto(detail.Trip.Id, detail.Trip.Status, detail.Trip.StartedOn, detail.Trip.EndedOn)
        {
            OwnerUserId = detail.Trip.OwnerUserId,
            Role = detail.Role,
            Title = detail.Trip.Title,
            PlaceName = detail.Trip.PlaceName,
            CatchCount = detail.Catches.Count,
            NoteCount = detail.Notes.Count,
            PhotographCount = detail.Photographs.Count
        };
    }
}
