using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Mappers;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Offline.Stores;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Offline.Synchronisers;

public sealed class TripMetadataSynchroniser(ITripStore store, ITripClient client) : IOutboxOperationSynchroniser
{
    public OutboxOperationTypeEnum OperationType => OutboxOperationTypeEnum.UpsertTripMetadata;

    public async Task<OutboxOperationResultModel> SynchroniseAsync(OutboxOperationModel operation, AccountSessionLeaseModel lease, CancellationToken cancellationToken)
    {
        var local = await store.GetAsync(lease.UserId, operation.AggregateId, cancellationToken);
        if (local is null || !local.HasPendingMetadata)
        {
            return OutboxOperationResultModel.Completed();
        }
        if (!local.CanManage)
        {
            return OutboxOperationResultModel.Terminal("TripUnavailable");
        }
        var trip = local.Detail.Trip;
        var sent = new TripDto(trip.Id, trip.Status, trip.StartedOn, trip.EndedOn, trip.Location)
        {
            OwnerUserId = lease.UserId,
            Title = trip.Title,
            PlaceName = trip.PlaceName
        };
        var response = await client.UpsertAsync(sent, cancellationToken);
        if (!response.IsSuccess)
        {
            return OutboxApiOutcomeMapper.ToFailure(response.Outcome);
        }
        var accepted = response.Value!;
        if (accepted.Id != trip.Id || accepted.OwnerUserId != lease.UserId)
        {
            return OutboxOperationResultModel.Retry("InvalidTripResponse");
        }
        var confirmed = local with
        {
            Detail = local.Detail with
            {
                Trip = trip with { Status = accepted.Status, EndedOn = accepted.EndedOn }
            }
        };
        return OutboxOperationResultModel.Completed((connection, transaction, token) =>
            store.ConfirmAsync(connection, transaction, confirmed, token));
    }
}
