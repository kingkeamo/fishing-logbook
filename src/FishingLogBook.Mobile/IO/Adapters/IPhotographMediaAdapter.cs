using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;

namespace FishingLogBook.Mobile.IO.Adapters;

public interface IPhotographMediaAdapter
{
    Task<PhotographPermissionStatusEnum> GetCameraStatusAsync(CancellationToken cancellationToken);

    Task<PhotographPermissionStatusEnum> RequestCameraAsync(CancellationToken cancellationToken);

    Task<PhotographPermissionStatusEnum> GetPhotosStatusAsync(CancellationToken cancellationToken);

    Task<PhotographPermissionStatusEnum> RequestPhotosAsync(CancellationToken cancellationToken);

    Task<PhotographPickResultModel> CapturePhotoAsync(CancellationToken cancellationToken);

    Task<PhotographPickResultModel> PickPhotosAsync(int maximumCount, CancellationToken cancellationToken);
}
