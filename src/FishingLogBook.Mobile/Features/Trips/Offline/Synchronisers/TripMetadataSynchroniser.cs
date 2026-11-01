using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Mappers;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
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
            PlannedStartOn = trip.PlannedStartOn,
            Title = trip.Title,
            PlaceName = trip.PlaceName
        };
        var response = await client.UpsertAsync(sent, cancellationToken);
        if (response.Outcome == ApiOutcomeEnum.Conflict)
        {
            return await ReconcileWithServerAsync(local, lease, cancellationToken);
        }
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
                Trip = trip with
                {
                    Status = accepted.Status,
                    StartedOn = accepted.StartedOn,
                    PlannedStartOn = accepted.PlannedStartOn,
                    EndedOn = accepted.EndedOn
                }
            }
        };
        return OutboxOperationResultModel.Completed((connection, transaction, token) =>
            store.ConfirmAsync(connection, transaction, confirmed, token));
    }

    private async Task<OutboxOperationResultModel> ReconcileWithServerAsync(LocalTripRecordModel local, AccountSessionLeaseModel lease, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync(local.Detail.Trip.Id, cancellationToken);
        if (!response.IsSuccess)
        {
            return response.Outcome == ApiOutcomeEnum.NotFound
                ? OutboxOperationResultModel.Retry("TripConflict")
                : OutboxApiOutcomeMapper.ToFailure(response.Outcome);
        }
        var server = response.Value!.Trip;
        if (server.Id != local.Detail.Trip.Id || server.OwnerUserId != lease.UserId)
        {
            return OutboxOperationResultModel.Terminal("TripUnavailable");
        }
        if (server.Status == FishingLogBook.Shared.Constants.TripConstants.Planned)
        {
            return OutboxOperationResultModel.Retry("TripConflict");
        }
        var rebased = new TripDto(server.Id, server.Status, server.StartedOn, server.EndedOn, local.Detail.Trip.Location)
        {
            OwnerUserId = lease.UserId,
            PlannedStartOn = server.PlannedStartOn,
            Title = local.Detail.Trip.Title,
            PlaceName = local.Detail.Trip.PlaceName
        };
        var saved = await client.UpsertAsync(rebased, cancellationToken);
        if (!saved.IsSuccess)
        {
            return OutboxApiOutcomeMapper.ToFailure(saved.Outcome);
        }
        var accepted = saved.Value!;
        if (accepted.Id != server.Id || accepted.OwnerUserId != lease.UserId)
        {
            return OutboxOperationResultModel.Retry("InvalidTripResponse");
        }
        var confirmed = local with
        {
            Detail = local.Detail with
            {
                Trip = local.Detail.Trip with
                {
                    Status = accepted.Status,
                    StartedOn = accepted.StartedOn,
                    PlannedStartOn = accepted.PlannedStartOn,
                    EndedOn = accepted.EndedOn
                }
            }
        };
        return OutboxOperationResultModel.Completed((connection, transaction, token) =>
            store.ConfirmAsync(connection, transaction, confirmed, token));
    }
}
