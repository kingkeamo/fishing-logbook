using System.Text.Json;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Mappers;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Photographs.Clients;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Stores;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Offline.Synchronisers;

public sealed class TripContributionSynchroniser(
    OutboxOperationTypeEnum operationType,
    ITripStore store,
    ITripClient client,
    ITripDependencyService dependency,
    IPhotographFileStore files,
    IFileSystemAdapter fileSystem,
    IPhotographUploadClient uploader) : IOutboxOperationSynchroniser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public OutboxOperationTypeEnum OperationType => operationType;

    public async Task<OutboxOperationResultModel> SynchroniseAsync(OutboxOperationModel operation, AccountSessionLeaseModel lease, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<TripOperationPayloadModel>(operation.PayloadJson, JsonOptions);
        if (payload is null || payload.TripId == Guid.Empty)
        {
            return OutboxOperationResultModel.Terminal("InvalidTripOperation");
        }
        var contribution = (await store.ListContributionsAsync(lease.UserId, payload.TripId, cancellationToken))
            .SingleOrDefault(item => item.Id == operation.AggregateId);
        if (contribution is null || !contribution.IsPending)
        {
            return OutboxOperationResultModel.Completed();
        }
        var readiness = await dependency.CheckAsync(lease.UserId, payload.TripId, cancellationToken);
        if (readiness is not null)
        {
            return readiness;
        }
        var failure = contribution.Deleted
            ? await DeleteAsync(contribution, cancellationToken)
            : await WriteAsync(contribution, cancellationToken);
        return failure ?? OutboxOperationResultModel.Completed((connection, transaction, token) =>
            store.ConfirmContributionAsync(connection, transaction, contribution, token));
    }

    private async Task<OutboxOperationResultModel?> DeleteAsync(LocalTripContributionModel contribution, CancellationToken cancellationToken)
    {
        var response = contribution.Note is not null
            ? await client.DeleteNoteAsync(contribution.TripId, contribution.Id, cancellationToken)
            : await client.DeletePhotographAsync(contribution.TripId, contribution.Id, cancellationToken);
        return response.IsSuccess || response.Outcome == ApiOutcomeEnum.NotFound
            ? null : ToFailure(response.Outcome);
    }

    private async Task<OutboxOperationResultModel?> WriteAsync(LocalTripContributionModel contribution, CancellationToken cancellationToken)
    {
        if (contribution.Note is { } note)
        {
            var result = await client.RecordNoteAsync(contribution.TripId,
                new RecordTripNoteDto(note.Id, note.Text, note.RecordedOn), cancellationToken);
            if (!result.IsSuccess)
            {
                return ToFailure(result.Outcome);
            }
            return result.Value!.Id == contribution.Id && result.Value.CreatedByUserId == contribution.UserId
                ? null : OutboxOperationResultModel.Retry("InvalidTripResponse");
        }
        return await WritePhotographAsync(contribution, cancellationToken);
    }

    private async Task<OutboxOperationResultModel?> WritePhotographAsync(LocalTripContributionModel contribution, CancellationToken cancellationToken)
    {
        var photo = contribution.Photograph;
        if (photo is null || contribution.RelativeFilePath is null
            || !files.TryResolveRelativePath(contribution.UserId, contribution.RelativeFilePath, out var path)
            || !fileSystem.FileExists(path))
        {
            return OutboxOperationResultModel.Terminal("PhotographFileMissing");
        }
        var upload = await client.CreatePhotographUploadAsync(contribution.TripId,
            new PhotographUploadRequestDto(photo.Id, photo.ContentType), cancellationToken);
        if (!upload.IsSuccess)
        {
            return ToFailure(upload.Outcome);
        }
        await using var stream = fileSystem.OpenRead(path);
        var uploaded = await uploader.UploadAsync(upload.Value!.UploadUrl, stream, photo.ContentType, cancellationToken);
        if (uploaded != PhotographUploadOutcomeEnum.Uploaded)
        {
            return OutboxOperationResultModel.Retry("PhotographUploadFailed");
        }
        var recorded = await client.RecordPhotographAsync(contribution.TripId,
            new RecordTripPhotographDto(photo.Id, upload.Value.ObjectKey, photo.ContentType, photo.AddedOn, photo.CapturedOn), cancellationToken);
        if (!recorded.IsSuccess)
        {
            return ToFailure(recorded.Outcome);
        }
        return recorded.Value!.Id == photo.Id && recorded.Value.TripId == contribution.TripId
            && recorded.Value.ContributedByUserId == contribution.UserId
            ? null : OutboxOperationResultModel.Retry("InvalidTripResponse");
    }

    private static OutboxOperationResultModel ToFailure(ApiOutcomeEnum outcome)
    {
        return outcome == ApiOutcomeEnum.NotFound
            ? OutboxOperationResultModel.Terminal("TripUnavailable")
            : OutboxApiOutcomeMapper.ToFailure(outcome);
    }
}
