using System.Diagnostics.CodeAnalysis;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Providers;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.Localization;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Native.Adapters;

[ExcludeFromCodeCoverage]
public sealed class PhotographMediaAdapter(UiTextService text, ILogger<PhotographMediaAdapter> logger) : IPhotographMediaAdapter
{
    public Task<PhotographPermissionStatusEnum> GetCameraStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MapStatusAsync(() => Permissions.CheckStatusAsync<Permissions.Camera>());
    }

    public Task<PhotographPermissionStatusEnum> RequestCameraAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MapStatusAsync(() => RequestAsync<Permissions.Camera>());
    }

    public Task<PhotographPermissionStatusEnum> GetPhotosStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MapStatusAsync(() => Permissions.CheckStatusAsync<Permissions.Photos>());
    }

    public Task<PhotographPermissionStatusEnum> RequestPhotosAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MapStatusAsync(() => RequestAsync<Permissions.Photos>());
    }

    public async Task<PhotographPickResultModel> CapturePhotoAsync(CancellationToken cancellationToken)
    {
        try
        {
            var permission = await EnsureCameraAsync(cancellationToken);
            if (permission != PhotographPermissionStatusEnum.Granted)
            {
                return MapPickFailure(permission);
            }

            var result = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = text["PhotoTakePicture"],
                CompressionQuality = IsIos() ? 85 : 100,
                MaximumWidth = IsIos() ? 2560 : null,
                MaximumHeight = IsIos() ? 2560 : null,
                RotateImage = IsIos(),
                PreserveMetaData = !IsIos()
            });
            return await ToPickResultAsync(result, cancellationToken);
        }
        catch (FeatureNotSupportedException)
        {
            return PhotographPickResultModel.Unavailable;
        }
        catch (PermissionException)
        {
            return PhotographPickResultModel.PermissionDenied;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PhotographPickResultModel.Cancelled;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(PhotographLoggerProvider.PipelineEvent, "Camera capture or cache copy failed with {ErrorType}.", exception.GetType().Name);
            return PhotographPickResultModel.Unavailable;
        }
    }

    public async Task<PhotographPickResultModel> PickPhotosAsync(int maximumCount, CancellationToken cancellationToken)
    {
        try
        {
            var permission = await EnsurePhotosAsync(cancellationToken);
            if (permission != PhotographPermissionStatusEnum.Granted)
            {
                return MapPickFailure(permission);
            }

            var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = text["PhotoChooseGallery"],
                FileTypes = FilePickerFileType.Images
            });
            if (results is null)
            {
                return PhotographPickResultModel.Cancelled;
            }

            var files = new List<PhotographPickedFileModel>();
            foreach (var result in results.Take(Math.Max(1, maximumCount)))
            {
                var mapped = await ToPickedFileAsync(result, cancellationToken);
                if (mapped is not null)
                {
                    files.Add(mapped);
                }
            }

            return files.Count == 0
                ? PhotographPickResultModel.Cancelled
                : PhotographPickResultModel.FromFiles(files);
        }
        catch (FeatureNotSupportedException)
        {
            return PhotographPickResultModel.Unavailable;
        }
        catch (PermissionException)
        {
            return PhotographPickResultModel.PermissionDenied;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PhotographPickResultModel.Cancelled;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(PhotographLoggerProvider.PipelineEvent, "Gallery selection or cache copy failed with {ErrorType}.", exception.GetType().Name);
            return PhotographPickResultModel.Unavailable;
        }
    }

    private async Task<PhotographPickResultModel> ToPickResultAsync(
        FileResult? result,
        CancellationToken cancellationToken)
    {
        if (result is null)
        {
            return PhotographPickResultModel.Cancelled;
        }

        var file = await ToPickedFileAsync(result, cancellationToken);
        return file is null
            ? PhotographPickResultModel.Cancelled
            : PhotographPickResultModel.FromFiles([file]);
    }

    private async Task<PhotographPickedFileModel?> ToPickedFileAsync(
        FileResult result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}{Path.GetExtension(result.FileName)}");
        long copiedBytes;
        await using (var source = await result.OpenReadAsync())
        await using (var destination = File.Create(path))
        {
            await source.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
            copiedBytes = destination.Length;
        }

        logger.LogInformation(PhotographLoggerProvider.PipelineEvent, "Photograph picker cache copy completed: {ByteCount} bytes, declared format {Format}.",
            copiedBytes, FishingLogBook.Shared.Constants.PhotographContentTypeConstants.IsAllowed(result.ContentType)
                ? result.ContentType : "other");

        return new PhotographPickedFileModel
        {
            FullPath = path,
            ContentType = string.IsNullOrWhiteSpace(result.ContentType) ? "image/jpeg" : result.ContentType,
            LastModified = null
        };
    }

    private Task<PhotographPermissionStatusEnum> EnsureCameraAsync(CancellationToken cancellationToken)
    {
        return EnsureAsync(
            GetCameraStatusAsync,
            RequestCameraAsync,
            Permissions.ShouldShowRationale<Permissions.Camera>(),
            cancellationToken);
    }

    private Task<PhotographPermissionStatusEnum> EnsurePhotosAsync(CancellationToken cancellationToken)
    {
        return EnsureAsync(
            GetPhotosStatusAsync,
            RequestPhotosAsync,
            Permissions.ShouldShowRationale<Permissions.Photos>(),
            cancellationToken);
    }

    private static async Task<PhotographPermissionStatusEnum> EnsureAsync(
        Func<CancellationToken, Task<PhotographPermissionStatusEnum>> getStatusAsync,
        Func<CancellationToken, Task<PhotographPermissionStatusEnum>> requestAsync,
        bool canPromptAgain,
        CancellationToken cancellationToken)
    {
        var status = await getStatusAsync(cancellationToken);
        if (PhotographPermissionGate.CanCapture(status))
        {
            return status;
        }

        if (PhotographPermissionGate.ShouldRequest(status, IsIos(), canPromptAgain))
        {
            return await requestAsync(cancellationToken);
        }

        return status;
    }

    private static bool IsIos()
    {
        return DeviceInfo.Current.Platform == DevicePlatform.iOS;
    }

    private static PhotographPickResultModel MapPickFailure(PhotographPermissionStatusEnum permission)
    {
        return permission switch
        {
            PhotographPermissionStatusEnum.Denied or PhotographPermissionStatusEnum.PermanentlyDenied =>
                PhotographPickResultModel.PermissionDenied,
            _ => PhotographPickResultModel.Unavailable
        };
    }

    private static async Task<PhotographPermissionStatusEnum> MapStatusAsync(Func<Task<PermissionStatus>> action)
    {
        try
        {
            return Map(await action());
        }
        catch (FeatureNotSupportedException)
        {
            return PhotographPermissionStatusEnum.Unavailable;
        }
        catch (PermissionException)
        {
            return PhotographPermissionStatusEnum.Denied;
        }
    }

    private static Task<PermissionStatus> RequestAsync<TPermission>()
        where TPermission : Permissions.BasePermission, new()
    {
        if (MainThread.IsMainThread)
        {
            return Permissions.RequestAsync<TPermission>();
        }

        return MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<TPermission>);
    }

    private static PhotographPermissionStatusEnum Map(PermissionStatus status)
    {
        return status switch
        {
            PermissionStatus.Granted or PermissionStatus.Limited => PhotographPermissionStatusEnum.Granted,
            PermissionStatus.Unknown => PhotographPermissionStatusEnum.NotDetermined,
            PermissionStatus.Denied => PhotographPermissionStatusEnum.Denied,
            PermissionStatus.Restricted or PermissionStatus.Disabled =>
                PhotographPermissionStatusEnum.PermanentlyDenied,
            _ => PhotographPermissionStatusEnum.Unavailable
        };
    }
}
