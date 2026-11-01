using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Mappers;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Common.Offline.Stores;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Offline.Stores;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public sealed class TripDependencyService(ITripStore store, ITripClient client, IOutboxStore outbox) : ITripDependencyService
{
    public async Task<OutboxOperationResultModel?> CheckAsync(Guid userId, Guid tripId, CancellationToken cancellationToken)
    {
        var local = await store.GetAsync(userId, tripId, cancellationToken);
        if (local?.AccessRevoked == true)
        {
            return OutboxOperationResultModel.Terminal("TripUnavailable");
        }
        if (local?.HasPendingMetadata == true)
        {
            var operations = await outbox.ListPendingForAggregateAsync(userId, OutboxAggregateTypeEnum.Trip, tripId, cancellationToken);
            return operations.Any(operation => operation.Status == OutboxStatusEnum.Failed)
                ? OutboxOperationResultModel.Terminal("TripSyncFailed")
                : OutboxOperationResultModel.Retry("WaitingForTrip");
        }
        var response = await client.GetAsync(tripId, cancellationToken);
        if (response.Outcome is ApiOutcomeEnum.NotFound or ApiOutcomeEnum.Forbidden)
        {
            return OutboxOperationResultModel.Terminal("TripUnavailable");
        }
        if (!response.IsSuccess)
        {
            return OutboxApiOutcomeMapper.ToFailure(response.Outcome);
        }
        return response.Value!.Role is TripParticipantConstants.Owner or TripParticipantConstants.Participant
            ? null
            : OutboxOperationResultModel.Terminal("TripUnavailable");
    }
}
