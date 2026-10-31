using System.Text.Json;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Common.Offline.Constants;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Mappers;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Common.Offline.Stores;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Photographs.Clients;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Synchronisers;

public sealed class CatchPhotographSynchroniser : IOutboxOperationSynchroniser
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly ICatchStore _catchStore;
    private readonly ICatchSyncClient _client;
    private readonly IPhotographUploadClient _uploadClient;
    private readonly ICatchSyncStateStore _state;
    private readonly IPhotographFileStore _files;
    private readonly IFileSystemAdapter _fileSystem;
    private readonly IOutboxStore _outbox;
    private readonly ISqliteDatabase _database;
    private readonly TimeProvider _time;

    public CatchPhotographSynchroniser(
        ICatchStore catchStore,
        ICatchSyncClient client,
        IPhotographUploadClient uploadClient,
        ICatchSyncStateStore state,
        IPhotographFileStore files,
        IFileSystemAdapter fileSystem,
        IOutboxStore outbox,
        ISqliteDatabase database,
        TimeProvider time)
    {
        _catchStore = catchStore;
        _client = client;
        _uploadClient = uploadClient;
        _state = state;
        _files = files;
        _fileSystem = fileSystem;
        _outbox = outbox;
        _database = database;
        _time = time;
    }

    public OutboxOperationTypeEnum OperationType => OutboxOperationTypeEnum.UploadCatchPhotograph;

    public async Task<OutboxOperationResultModel> SynchroniseAsync(
        OutboxOperationModel operation,
        AccountSessionLeaseModel lease,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PhotographUploadPayloadModel>(operation.PayloadJson, PayloadOptions);
        if (payload is null)
        {
            return OutboxOperationResultModel.Terminal(OutboxErrorCodeConstants.Unexpected);
        }

        var local = await _catchStore.GetForOwnerAsync(lease.UserId, payload.CatchId, cancellationToken);
        var photograph = local?.Photographs.FirstOrDefault(item => item.Id == operation.AggregateId);
        if (local is null || local.Lifecycle != LocalCatchLifecycleEnum.Saved || photograph is null)
        {
            return OutboxOperationResultModel.Completed();
        }

        var state = await _state.GetPhotographStateAsync(lease.UserId, photograph.Id, cancellationToken);
        if (state?.Status == PhotographSyncStatusEnum.Recorded)
        {
            return OutboxOperationResultModel.Completed();
        }

        if (await _state.GetSyncedRevisionAsync(lease.UserId, local.Id, cancellationToken) == 0)
        {
            return OutboxOperationResultModel.Retry(OutboxErrorCodeConstants.WaitingForCatch);
        }

        if (string.IsNullOrWhiteSpace(photograph.ContentType))
        {
            return OutboxOperationResultModel.Terminal(OutboxErrorCodeConstants.PhotographContentTypeMissing);
        }

        var objectKey = state?.Status == PhotographSyncStatusEnum.Uploaded ? state.ObjectKey : null;
        if (objectKey is null)
        {
            var uploaded = await UploadAsync(operation, local, photograph, cancellationToken);
            if (uploaded.Failure is not null)
            {
                return uploaded.Failure;
            }

            objectKey = uploaded.ObjectKey;
        }

        return await RecordAsync(operation, local, photograph, objectKey!, cancellationToken);
    }

    private async Task<UploadStepModel> UploadAsync(
        OutboxOperationModel operation,
        LocalCatchRecordModel local,
        LocalCatchPhotographModel photograph,
        CancellationToken cancellationToken)
    {
        if (photograph.LocalFilePath is null
            || !_files.TryResolveRelativePath(local.UserId, photograph.LocalFilePath, out var absolutePath)
            || !_fileSystem.FileExists(absolutePath))
        {
            return UploadStepModel.Failed(
                OutboxOperationResultModel.Terminal(OutboxErrorCodeConstants.PhotographFileMissing));
        }

        var upload = await _client.CreatePhotographUploadAsync(
            local.Id,
            new PhotographUploadRequestDto(photograph.Id, photograph.ContentType!),
            cancellationToken);
        if (!upload.IsSuccess)
        {
            return UploadStepModel.Failed(await ToFailureAsync(operation, local, upload.Outcome, cancellationToken));
        }

        PhotographUploadOutcomeEnum outcome;
        await using (var stream = _fileSystem.OpenRead(absolutePath))
        {
            outcome = await _uploadClient.UploadAsync(
                upload.Value!.UploadUrl,
                stream,
                photograph.ContentType!,
                cancellationToken);
        }

        if (outcome != PhotographUploadOutcomeEnum.Uploaded)
        {
            return UploadStepModel.Failed(OutboxOperationResultModel.Retry(
                outcome == PhotographUploadOutcomeEnum.UrlRejected
                    ? OutboxErrorCodeConstants.UploadUrlRejected
                    : ApiOutcomeEnum.Unavailable.ToString()));
        }

        await _state.SetPhotographUploadedAsync(
            new PhotographSyncStateModel
            {
                PhotographId = photograph.Id,
                CatchId = local.Id,
                UserId = local.UserId,
                Status = PhotographSyncStatusEnum.Uploaded,
                ObjectKey = upload.Value.ObjectKey
            },
            _time.GetUtcNow(),
            cancellationToken);
        return UploadStepModel.Succeeded(upload.Value.ObjectKey);
    }

    private async Task<OutboxOperationResultModel> RecordAsync(
        OutboxOperationModel operation,
        LocalCatchRecordModel local,
        LocalCatchPhotographModel photograph,
        string objectKey,
        CancellationToken cancellationToken)
    {
        var recorded = await _client.RecordPhotographAsync(
            local.Id,
            new RecordPhotographDto(photograph.Id, objectKey, photograph.ContentType!),
            cancellationToken);
        if (!recorded.IsSuccess)
        {
            return await ToFailureAsync(operation, local, recorded.Outcome, cancellationToken);
        }

        return OutboxOperationResultModel.Completed(
            (connection, transaction, token) => _state.SetPhotographStateAsync(
                connection,
                transaction,
                new PhotographSyncStateModel
                {
                    PhotographId = photograph.Id,
                    CatchId = local.Id,
                    UserId = local.UserId,
                    Status = PhotographSyncStatusEnum.Recorded,
                    ObjectKey = objectKey
                },
                _time.GetUtcNow(),
                token));
    }

    private async Task<OutboxOperationResultModel> ToFailureAsync(
        OutboxOperationModel operation,
        LocalCatchRecordModel local,
        ApiOutcomeEnum outcome,
        CancellationToken cancellationToken)
    {
        if (outcome != ApiOutcomeEnum.NotFound)
        {
            return OutboxApiOutcomeMapper.ToFailure(outcome);
        }

        if (operation.LastError == OutboxErrorCodeConstants.NotFound)
        {
            return OutboxOperationResultModel.Terminal(OutboxErrorCodeConstants.NotFound);
        }

        await _state.ClearSyncedRevisionAsync(local.UserId, local.Id, cancellationToken);
        var now = _time.GetUtcNow();
        await _database.ExecuteInTransactionAsync(
            (connection, transaction, token) => _outbox.UpsertPendingAsync(
                connection,
                transaction,
                new OutboxOperationModel
                {
                    Id = Guid.NewGuid(),
                    UserId = local.UserId,
                    AggregateType = OutboxAggregateTypeEnum.Catch,
                    AggregateId = local.Id,
                    OperationType = OutboxOperationTypeEnum.UpsertCatchMetadata,
                    PayloadJson = "{}",
                    Status = OutboxStatusEnum.Pending,
                    AttemptCount = 0,
                    CreatedOn = now,
                    AvailableAt = now
                },
                token),
            cancellationToken);
        return OutboxOperationResultModel.Retry(OutboxErrorCodeConstants.NotFound);
    }

    private sealed record UploadStepModel(OutboxOperationResultModel? Failure, string? ObjectKey)
    {
        public static UploadStepModel Failed(OutboxOperationResultModel failure)
        {
            return new UploadStepModel(failure, null);
        }

        public static UploadStepModel Succeeded(string objectKey)
        {
            return new UploadStepModel(null, objectKey);
        }
    }
}
