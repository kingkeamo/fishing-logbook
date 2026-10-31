using System.Text.Json;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Common.Offline.Constants;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Mappers;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Common.Offline.Stores;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Shared.Dtos;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Synchronisers;

public sealed class CatchMetadataSynchroniser : IOutboxOperationSynchroniser
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly ICatchStore _catchStore;
    private readonly ICatchSyncClient _client;
    private readonly ICatchSyncStateStore _state;
    private readonly IOutboxStore _outbox;
    private readonly TimeProvider _time;

    public CatchMetadataSynchroniser(
        ICatchStore catchStore,
        ICatchSyncClient client,
        ICatchSyncStateStore state,
        IOutboxStore outbox,
        TimeProvider time)
    {
        _catchStore = catchStore;
        _client = client;
        _state = state;
        _outbox = outbox;
        _time = time;
    }

    public OutboxOperationTypeEnum OperationType => OutboxOperationTypeEnum.UpsertCatchMetadata;

    public async Task<OutboxOperationResultModel> SynchroniseAsync(
        OutboxOperationModel operation,
        AccountSessionLeaseModel lease,
        CancellationToken cancellationToken)
    {
        var local = await _catchStore.GetForOwnerAsync(lease.UserId, operation.AggregateId, cancellationToken);
        if (local is null || local.Lifecycle != LocalCatchLifecycleEnum.Saved)
        {
            return OutboxOperationResultModel.Completed();
        }

        if (local.Photographs.Any(photograph => string.IsNullOrWhiteSpace(photograph.ContentType)))
        {
            return OutboxOperationResultModel.Terminal(OutboxErrorCodeConstants.PhotographContentTypeMissing);
        }

        var removal = await RemoveDeletedPhotographsAsync(lease.UserId, local, cancellationToken);
        if (removal is not null)
        {
            return removal;
        }

        var recorded = (await _state.ListPhotographStatesAsync(lease.UserId, local.Id, cancellationToken))
            .Where(state => state.Status == PhotographSyncStatusEnum.Recorded)
            .Select(state => state.PhotographId)
            .ToHashSet();
        var syncedRevision = await _state.GetSyncedRevisionAsync(lease.UserId, local.Id, cancellationToken);
        if (syncedRevision >= local.Revision)
        {
            return OutboxOperationResultModel.Completed(
                (connection, transaction, token) => PersistAsync(connection, transaction, local, recorded, token));
        }

        var response = await _client.UpsertAsync(ToDto(local), cancellationToken);
        return response.IsSuccess
            ? OutboxOperationResultModel.Completed(
                (connection, transaction, token) => PersistAsync(connection, transaction, local, recorded, token))
            : OutboxApiOutcomeMapper.ToFailure(response.Outcome);
    }

    private async Task<OutboxOperationResultModel?> RemoveDeletedPhotographsAsync(
        Guid userId,
        LocalCatchRecordModel local,
        CancellationToken cancellationToken)
    {
        var localIds = local.Photographs.Select(photograph => photograph.Id).ToHashSet();
        var states = await _state.ListPhotographStatesAsync(userId, local.Id, cancellationToken);
        foreach (var removed in states.Where(state => !localIds.Contains(state.PhotographId)))
        {
            if (removed.Status != PhotographSyncStatusEnum.Pending)
            {
                var deleted = await _client.DeletePhotographAsync(local.Id, removed.PhotographId, cancellationToken);
                if (!deleted.IsSuccess && deleted.Outcome != ApiOutcomeEnum.NotFound)
                {
                    return OutboxApiOutcomeMapper.ToFailure(deleted.Outcome);
                }
            }

            await _state.DeletePhotographStateAsync(userId, removed.PhotographId, cancellationToken);
        }

        return null;
    }

    private async Task PersistAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LocalCatchRecordModel local,
        IReadOnlySet<Guid> recorded,
        CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        await _state.RecordSyncedRevisionAsync(
            connection,
            transaction,
            local.UserId,
            local.Id,
            local.Revision,
            now,
            cancellationToken);
        foreach (var photograph in local.Photographs.Where(photograph => !recorded.Contains(photograph.Id)))
        {
            await _state.EnsurePhotographPendingAsync(
                connection,
                transaction,
                local.UserId,
                local.Id,
                photograph.Id,
                now,
                cancellationToken);
            await _outbox.UpsertPendingAsync(
                connection,
                transaction,
                new OutboxOperationModel
                {
                    Id = Guid.NewGuid(),
                    UserId = local.UserId,
                    AggregateType = OutboxAggregateTypeEnum.CatchPhotograph,
                    AggregateId = photograph.Id,
                    OperationType = OutboxOperationTypeEnum.UploadCatchPhotograph,
                    PayloadJson = JsonSerializer.Serialize(new PhotographUploadPayloadModel(local.Id), PayloadOptions),
                    Status = OutboxStatusEnum.Pending,
                    AttemptCount = 0,
                    CreatedOn = now,
                    AvailableAt = now
                },
                cancellationToken);
        }
    }

    private static CatchDto ToDto(LocalCatchRecordModel local)
    {
        var photographs = local.Photographs
            .OrderBy(photograph => photograph.SortOrder)
            .Select(photograph => new CatchPhotographDto(photograph.Id, local.Id, photograph.ContentType!))
            .ToArray();
        var location = local.Location is null
            ? null
            : new CatchLocationDto(
                local.Location.Latitude,
                local.Location.Longitude,
                local.Location.AccuracyMetres,
                local.Location.CapturedOn,
                local.Location.Source,
                local.Location.Visibility,
                local.Location.ConsentVersion);
        return new CatchDto(local.Id, local.CaughtOn, photographs, location)
        {
            CaughtByUserId = local.CaughtByUserId,
            RecordedByUserId = local.RecordedByUserId,
            TripId = local.TripId,
            SpeciesName = local.SpeciesName,
            Weight = (decimal?)local.Weight,
            Length = (decimal?)local.Length,
            Method = local.Method,
            BaitOrLure = local.BaitOrLure,
            Notes = local.Notes,
            PlaceName = local.PlaceName
        };
    }
}
