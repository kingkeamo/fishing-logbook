using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;

namespace FishingLogBook.Web.Features.Trips.Services;

public sealed class PlannedTripService : IPlannedTripService
{
    private readonly ITripStore _tripStore;
    private readonly ITripClient _tripClient;
    private readonly INetworkService _network;
    private readonly IActiveTripService _activeTrip;
    private readonly ILoggingService _logging;
    private readonly TimeProvider _timeProvider;

    public PlannedTripService(
        ITripStore tripStore,
        ITripClient tripClient,
        INetworkService network,
        IActiveTripService activeTrip,
        ILoggingService logging,
        TimeProvider timeProvider)
    {
        _tripStore = tripStore;
        _tripClient = tripClient;
        _network = network;
        _activeTrip = activeTrip;
        _logging = logging;
        _timeProvider = timeProvider;
    }

    public async Task<TripModel> PlanAsync(
        Guid ownerUserId,
        string? title,
        string? placeName,
        DateTimeOffset plannedStartOn,
        CancellationToken cancellationToken)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip requires an owner.");
        }

        var trip = new TripModel(
            Guid.NewGuid(),
            ownerUserId,
            TripConstants.Planned,
            plannedStartOn,
            Title: TripConstants.TrimTitle(title),
            PlaceName: TripConstants.TrimPlaceName(placeName),
            SyncStatus: SyncStatus.SavedLocally,
            PlannedStartOn: plannedStartOn);
        await _tripStore.SaveAsync(trip, cancellationToken);
        return trip;
    }

    public async Task<TripModel?> UpdatePlanAsync(
        Guid ownerUserId,
        Guid tripId,
        string? title,
        string? placeName,
        DateTimeOffset plannedStartOn,
        CancellationToken cancellationToken)
    {
        var current = await _tripStore.GetAsync(ownerUserId, tripId, cancellationToken);
        if (current is not { IsPlanned: true } || !current.IsOwnedBy(ownerUserId))
        {
            return null;
        }

        var updated = current with
        {
            Title = TripConstants.TrimTitle(title),
            PlaceName = TripConstants.TrimPlaceName(placeName),
            StartedOn = plannedStartOn,
            PlannedStartOn = plannedStartOn,
            SyncStatus = SyncStatus.SavedLocally
        };
        await _tripStore.SaveAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<TripModel?> CancelPlanAsync(Guid ownerUserId, Guid tripId, CancellationToken cancellationToken)
    {
        var current = await _tripStore.GetAsync(ownerUserId, tripId, cancellationToken);
        if (current is not { IsPlanned: true } || !current.IsOwnedBy(ownerUserId))
        {
            return null;
        }

        var cancelled = current with { Status = TripConstants.Cancelled, SyncStatus = SyncStatus.SavedLocally };
        await _tripStore.SaveAsync(cancelled, cancellationToken);
        return cancelled;
    }

    public async Task<IReadOnlyList<PlannedTripOptionModel>> GetEligibleAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        if (viewerUserId == Guid.Empty)
        {
            return [];
        }

        var now = _timeProvider.GetUtcNow();
        var local = await ReadLocalAsync(viewerUserId, now, cancellationToken);
        var server = await ReadServerAsync(now, cancellationToken);
        var serverIds = server.Select(option => option.TripId).ToHashSet();
        return
        [
            .. server.Concat(local.Where(option => !serverIds.Contains(option.TripId)))
                .OrderBy(option => option.PlannedStartOn)
                .ThenBy(option => option.TripId)
        ];
    }

    public async Task<TripModel> ActivateAsync(
        Guid viewerUserId,
        PlannedTripOptionModel option,
        CancellationToken cancellationToken)
    {
        if (option.IsOrganiser)
        {
            await EnsureLocalCopyAsync(viewerUserId, option.TripId, cancellationToken);
            return await _activeTrip.ActivatePlannedAsync(viewerUserId, option.TripId, cancellationToken);
        }

        var activated = await _tripClient.ActivateAsync(option.TripId, cancellationToken)
            ?? throw new InvalidOperationException("The planned trip could not be started.");
        _activeTrip.Invalidate();
        return new TripModel(
            activated.Id,
            activated.OwnerUserId,
            activated.Status,
            activated.StartedOn,
            activated.EndedOn,
            activated.Title,
            activated.PlaceName,
            SyncStatus: SyncStatus.Synchronised,
            ParticipantUserIds: [viewerUserId],
            Origin: TripOriginEnum.Server,
            PlannedStartOn: activated.PlannedStartOn);
    }

    private async Task EnsureLocalCopyAsync(Guid ownerUserId, Guid tripId, CancellationToken cancellationToken)
    {
        if (await _tripStore.GetAsync(ownerUserId, tripId, cancellationToken) is not null)
        {
            return;
        }

        var detail = await _tripClient.GetDetailAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException("The planned trip could not be found.");
        await _tripStore.SaveAsync(
            new TripModel(
                detail.Trip.Id,
                detail.Trip.OwnerUserId,
                detail.Trip.Status,
                detail.Trip.StartedOn,
                detail.Trip.EndedOn,
                detail.Trip.Title,
                detail.Trip.PlaceName,
                SyncStatus: SyncStatus.Synchronised,
                SyncedAt: _timeProvider.GetUtcNow(),
                PlannedStartOn: detail.Trip.PlannedStartOn),
            cancellationToken);
    }

    private async Task<IReadOnlyList<PlannedTripOptionModel>> ReadLocalAsync(
        Guid viewerUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            var trips = await _tripStore.GetAllAsync(viewerUserId, cancellationToken);
            return
            [
                .. trips
                    .Where(trip => trip.IsPlanned
                        && trip.IsOwnedBy(viewerUserId)
                        && trip.PlannedStartOn is { } plannedStartOn
                        && TripConstants.IsWithinPlannedWindow(plannedStartOn, now, TripConstants.DefaultPlannedWindowDays))
                    .Select(trip => new PlannedTripOptionModel(
                        trip.Id,
                        trip.PlannedStartOn!.Value,
                        trip.OwnerUserId,
                        OrganiserName: null,
                        IsOrganiser: true)
                    {
                        Title = trip.Title,
                        PlaceName = trip.PlaceName
                    })
            ];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _logging.LogErrorAsync("reading the local planned trips", exception, CancellationToken.None);
            return [];
        }
    }

    private async Task<IReadOnlyList<PlannedTripOptionModel>> ReadServerAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await _network.IsOnlineAsync(cancellationToken))
            {
                return [];
            }

            var options = await _tripClient.GetPlannedAsync(windowDays: null, cancellationToken);
            return
            [
                .. options
                    .Where(option => TripConstants.IsWithinPlannedWindow(
                        option.PlannedStartOn,
                        now,
                        TripConstants.DefaultPlannedWindowDays))
                    .Select(ToModel)
            ];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _logging.LogErrorAsync("reading the planned trips", exception, CancellationToken.None);
            return [];
        }
    }

    private static PlannedTripOptionModel ToModel(PlannedTripOptionDto option)
    {
        return new PlannedTripOptionModel(
            option.TripId,
            option.PlannedStartOn,
            option.OrganiserUserId,
            option.OrganiserName,
            option.Role == TripParticipantConstants.Owner)
        {
            Title = option.Title,
            PlaceName = option.PlaceName,
            Guests =
            [
                .. option.Participants.Select(participant => new PlannedTripGuestModel(
                    participant.UserId,
                    participant.DisplayName,
                    participant.Status == TripParticipantConstants.Pending))
            ]
        };
    }
}
