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

public sealed class PlannedTripService(ITripStore store, ITripClient client, ITripService trips, IAccountSessionCoordinator session,
    IConnectivityService connectivity, TimeProvider time) : IPlannedTripService
{
    public async Task<LocalTripRecordModel> PlanAsync(string? title, string? placeName, DateTimeOffset plannedStartOn, CancellationToken cancellationToken)
    {
        var lease = RequireLease();
        RequireValidStart(plannedStartOn);
        var trip = new LocalTripRecordModel
        {
            UserId = lease.UserId,
            Detail = new TripDetailDto(new TripViewDto(Guid.NewGuid(), lease.UserId, TripConstants.Planned, plannedStartOn)
            {
                PlannedStartOn = plannedStartOn,
                Title = TripConstants.TrimTitle(title),
                PlaceName = TripConstants.TrimPlaceName(placeName)
            })
            { Role = TripParticipantConstants.Owner }
        };
        await store.SaveAsync(lease, trip, 0, cancellationToken);
        return trip with { Revision = 1 };
    }

    public async Task<LocalTripRecordModel> UpdatePlanAsync(LocalTripRecordModel trip, string? title, string? placeName, DateTimeOffset plannedStartOn, CancellationToken cancellationToken)
    {
        var lease = RequireLease();
        RequireOwnedPlan(lease, trip);
        RequireValidStart(plannedStartOn);
        return await SaveAsync(lease, trip, trip.Detail.Trip with
        {
            Title = TripConstants.TrimTitle(title),
            PlaceName = TripConstants.TrimPlaceName(placeName),
            StartedOn = plannedStartOn,
            PlannedStartOn = plannedStartOn
        }, cancellationToken);
    }

    public async Task<LocalTripRecordModel> CancelPlanAsync(LocalTripRecordModel trip, CancellationToken cancellationToken)
    {
        var lease = RequireLease();
        RequireOwnedPlan(lease, trip);
        return await SaveAsync(lease, trip, trip.Detail.Trip with { Status = TripConstants.Cancelled }, cancellationToken);
    }

    public async Task<PlannedTripLookupModel> GetEligibleAsync(CancellationToken cancellationToken)
    {
        var lease = RequireLease();
        var now = time.GetUtcNow();
        var local = (await store.ListAsync(lease.UserId, cancellationToken))
            .Where(trip => trip is { IsPlanned: true, CanManage: true } && IsEligible(trip.Detail.Trip.PlannedStartOn, now))
            .Select(ToOption)
            .ToArray();
        var (server, failed) = await ReadServerAsync(now, cancellationToken);
        var serverIds = server.Select(option => option.TripId).ToHashSet();
        IReadOnlyList<PlannedTripOptionModel> merged = [.. server.Concat(local.Where(option => !serverIds.Contains(option.TripId)))
            .OrderBy(option => option.PlannedStartOn).ThenBy(option => option.TripId)];
        return failed ? PlannedTripLookupModel.Unavailable(merged) : PlannedTripLookupModel.Succeeded(merged);
    }

    public async Task<Guid> ActivateAsync(PlannedTripOptionModel option, CancellationToken cancellationToken)
    {
        var lease = RequireLease();
        if (option.IsOrganiser)
        {
            return await ActivateOwnedAsync(lease, option.TripId, cancellationToken);
        }

        var response = await client.ActivateAsync(option.TripId, cancellationToken);
        if (!response.IsSuccess)
        {
            throw new InvalidOperationException("The planned trip could not be started.");
        }

        await trips.GetAsync(option.TripId, cancellationToken);
        return option.TripId;
    }

    private async Task<Guid> ActivateOwnedAsync(AccountSessionLeaseModel lease, Guid tripId, CancellationToken cancellationToken)
    {
        var existing = await store.ListAsync(lease.UserId, cancellationToken);
        var plan = existing.FirstOrDefault(trip => trip.Detail.Trip.Id == tripId);
        if (plan is null)
        {
            plan = await trips.GetAsync(tripId, cancellationToken);
        }

        if (plan is not { IsPlanned: true, CanManage: true } || plan.UserId != lease.UserId)
        {
            throw new InvalidOperationException("Only the organiser can start a planned trip.");
        }

        if (existing.Any(trip => trip.IsActive && trip.CanManage))
        {
            throw new InvalidOperationException("An owned trip is already active.");
        }

        await SaveAsync(lease, plan, plan.Detail.Trip with { Status = TripConstants.Active, StartedOn = time.GetUtcNow() }, cancellationToken);
        return tripId;
    }

    private async Task<LocalTripRecordModel> SaveAsync(AccountSessionLeaseModel lease, LocalTripRecordModel trip, TripViewDto edited, CancellationToken cancellationToken)
    {
        var saved = trip with { Detail = trip.Detail with { Trip = edited } };
        await store.HydrateAsync(lease, trip, cancellationToken);
        var current = await store.GetAsync(lease.UserId, edited.Id, cancellationToken);
        await store.SaveAsync(lease, saved, current!.Revision, cancellationToken);
        return saved with { Revision = current.Revision + 1, SyncedRevision = current.SyncedRevision };
    }

    private async Task<(IReadOnlyList<PlannedTripOptionModel> Options, bool Failed)> ReadServerAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!connectivity.IsOnline)
        {
            return ([], false);
        }

        var response = await client.ListPlannedAsync(null, cancellationToken);
        if (!response.IsSuccess)
        {
            return ([], true);
        }

        return ([.. response.Value!.Where(option => IsEligible(option.PlannedStartOn, now)).Select(ToOption)], false);
    }

    private static bool IsEligible(DateTimeOffset? plannedStartOn, DateTimeOffset now)
    {
        return plannedStartOn is { } planned
            && TripConstants.IsWithinPlannedWindow(planned, now, TripConstants.DefaultPlannedWindowDays);
    }

    private static PlannedTripOptionModel ToOption(LocalTripRecordModel trip)
    {
        var detail = trip.Detail.Trip;
        return new PlannedTripOptionModel(detail.Id, detail.PlannedStartOn!.Value, detail.OwnerUserId, null, IsOrganiser: true)
        {
            Title = detail.Title,
            PlaceName = detail.PlaceName
        };
    }

    private static PlannedTripOptionModel ToOption(PlannedTripOptionDto option)
    {
        return new PlannedTripOptionModel(option.TripId, option.PlannedStartOn, option.OrganiserUserId, option.OrganiserName,
            option.Role == TripParticipantConstants.Owner)
        {
            Title = option.Title,
            PlaceName = option.PlaceName,
            Guests = [.. option.Participants.Select(guest =>
                new PlannedTripGuestModel(guest.UserId, guest.DisplayName, guest.Status == TripParticipantConstants.Pending))]
        };
    }

    private static void RequireOwnedPlan(AccountSessionLeaseModel lease, LocalTripRecordModel trip)
    {
        if (trip.UserId != lease.UserId || !trip.CanManage || !trip.IsPlanned)
        {
            throw new UnauthorizedAccessException();
        }
    }

    private static void RequireValidStart(DateTimeOffset plannedStartOn)
    {
        if (!TripConstants.IsPlannedStartOnValid(plannedStartOn))
        {
            throw new ArgumentException("Invalid planned start.", nameof(plannedStartOn));
        }
    }

    private AccountSessionLeaseModel RequireLease()
    {
        return session.Capture() ?? throw new UnauthorizedAccessException();
    }
}
