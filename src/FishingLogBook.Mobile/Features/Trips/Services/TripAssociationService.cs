using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Stores;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public sealed class TripAssociationService(
    ITripService trips,
    ITripStore store,
    ITripParticipantClient participantClient,
    IAccountSessionCoordinator session,
    IConnectivityService connectivity) : ITripAssociationService
{
    public async Task<TripAssociationModel?> ResolveAsync(Guid? requestedTripId, CancellationToken cancellationToken)
    {
        if (session.Capture() is not { } lease)
        {
            return null;
        }

        var tripId = requestedTripId is { } requested && requested != Guid.Empty
            ? requested
            : await FindActiveTripIdAsync(cancellationToken);
        if (tripId is null)
        {
            return null;
        }

        var trip = await trips.GetAsync(tripId.Value, cancellationToken);
        if (trip is null || !trip.CanContribute)
        {
            return null;
        }

        var anglers = await ListAnglersAsync(lease.UserId, trip, cancellationToken);
        var detail = trip.Detail.Trip;
        return new TripAssociationModel(detail.Id, detail.Title, detail.PlaceName, detail.StartedOn, anglers);
    }

    public async Task<bool> IsAvailableAsync(Guid tripId, CancellationToken cancellationToken)
    {
        if (session.Capture() is not { } lease)
        {
            return false;
        }

        var trip = await store.GetAsync(lease.UserId, tripId, cancellationToken);
        return trip is { CanContribute: true };
    }

    private async Task<Guid?> FindActiveTripIdAsync(CancellationToken cancellationToken)
    {
        var history = await trips.LoadAsync(cancellationToken);
        return history.Items
            .Where(trip => trip.Status == TripConstants.Active
                && trip.Role is TripParticipantConstants.Owner or TripParticipantConstants.Participant)
            .OrderByDescending(trip => trip.Role == TripParticipantConstants.Owner)
            .Select(trip => (Guid?)trip.Id)
            .FirstOrDefault();
    }

    private async Task<IReadOnlyList<TripAnglerOptionModel>> ListAnglersAsync(
        Guid userId,
        LocalTripRecordModel trip,
        CancellationToken cancellationToken)
    {
        var self = new TripAnglerOptionModel(userId, null, IsSelf: true);
        if (!connectivity.IsOnline)
        {
            return [self];
        }

        var response = await participantClient.GetAsync(trip.Detail.Trip.Id, cancellationToken);
        if (!response.IsSuccess)
        {
            return [self];
        }

        var others = response.Value!.Participants
            .Where(participant => participant.UserId != userId
                && (participant.IsOwner || participant.Status == TripParticipantConstants.Accepted))
            .Select(participant => new TripAnglerOptionModel(participant.UserId, participant.DisplayName, IsSelf: false));
        return [self, .. others];
    }
}
