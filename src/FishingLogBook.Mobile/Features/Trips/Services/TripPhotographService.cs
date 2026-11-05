using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public sealed class TripPhotographService(
    ITripService trips,
    IPhotographCollectionEditorService photographs,
    IAccountSessionCoordinator session,
    TimeProvider time,
    ILogger<TripPhotographService> logger) : ITripPhotographService
{
    public async Task<string?> AddAsync(LocalTripRecordModel trip, bool useCamera, CancellationToken cancellationToken)
    {
        if (session.Capture() is not { } lease)
        {
            return "TripPhotographAddFailed";
        }

        var result = useCamera
            ? await photographs.TakePhotoAsync(lease.UserId, [], 1, cancellationToken)
            : await photographs.ChooseFromGalleryAsync(lease.UserId, [], 1, cancellationToken);
        if (result.Outcome != PhotographPreparationOutcomeEnum.Prepared || result.Items.Count != 1)
        {
            return result.Outcome switch
            {
                PhotographPreparationOutcomeEnum.Cancelled => null,
                PhotographPreparationOutcomeEnum.CameraPermissionDenied => "PhotoCameraAccessDenied",
                PhotographPreparationOutcomeEnum.PhotoLibraryPermissionDenied => "PhotoLibraryAccessDenied",
                PhotographPreparationOutcomeEnum.PermissionDenied => "PhotoPermissionDenied",
                PhotographPreparationOutcomeEnum.UnsupportedContentType => "PhotoUnsupportedFormat",
                PhotographPreparationOutcomeEnum.TooLarge => "PhotoTooLarge",
                _ => "PhotoPrepareFailed"
            };
        }

        var item = result.Items[0];
        var photograph = new TripPhotographViewDto(item.Id, item.ContentType, time.GetUtcNow(), null, item.Metadata.CapturedOn)
        {
            ContributedByUserId = lease.UserId
        };
        try
        {
            await trips.SavePhotographAsync(trip, photograph, item.RelativeFilePath, deleted: false, cancellationToken);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to save trip photograph {PhotographId}.", item.Id);
            return "TripPhotographAddFailed";
        }
    }

    public async Task<bool> RemoveAsync(LocalTripRecordModel trip, Guid photographId, CancellationToken cancellationToken)
    {
        if (session.Capture() is not { } lease
            || trip.Detail.Photographs.FirstOrDefault(photograph => photograph.Id == photographId) is not { } existing
            || existing.ContributedByUserId != lease.UserId)
        {
            return false;
        }

        try
        {
            await trips.SavePhotographAsync(trip, existing, null, deleted: true, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to remove trip photograph {PhotographId}.", photographId);
            return false;
        }
    }
}
