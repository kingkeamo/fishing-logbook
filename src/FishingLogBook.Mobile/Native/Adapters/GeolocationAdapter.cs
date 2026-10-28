using System.Diagnostics.CodeAnalysis;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.IO.Enums;
using FishingLogBook.Mobile.IO.Models;

namespace FishingLogBook.Mobile.Native.Adapters;

[ExcludeFromCodeCoverage]
public sealed class GeolocationAdapter : IGeolocationAdapter
{
    public async Task<DeviceLocationPermissionStatusEnum> GetWhenInUseStatusAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Map(await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>());
        }
        catch (FeatureNotSupportedException)
        {
            return DeviceLocationPermissionStatusEnum.Unavailable;
        }
        catch (PermissionException)
        {
            return DeviceLocationPermissionStatusEnum.Denied;
        }
    }

    public async Task<DeviceLocationPermissionStatusEnum> RequestWhenInUseAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Map(await RequestAsync());
        }
        catch (FeatureNotSupportedException)
        {
            return DeviceLocationPermissionStatusEnum.Unavailable;
        }
        catch (PermissionException)
        {
            return DeviceLocationPermissionStatusEnum.Denied;
        }
    }

    public async Task<DeviceGpsReadModel> GetCurrentAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            var request = new GeolocationRequest(GeolocationAccuracy.Best, timeout);
            var location = await Geolocation.Default.GetLocationAsync(request, timeoutSource.Token);
            if (location is null)
            {
                return DeviceGpsReadModel.Failed(DeviceGpsReadOutcomeEnum.Unavailable);
            }

            return DeviceGpsReadModel.Captured(new DeviceGpsFixModel(
                location.Latitude,
                location.Longitude,
                AccuracyOrNull(location.Accuracy),
                location.Timestamp));
        }
        catch (FeatureNotSupportedException)
        {
            return DeviceGpsReadModel.Failed(DeviceGpsReadOutcomeEnum.Unavailable);
        }
        catch (FeatureNotEnabledException)
        {
            return DeviceGpsReadModel.Failed(DeviceGpsReadOutcomeEnum.Unavailable);
        }
        catch (PermissionException)
        {
            return DeviceGpsReadModel.Failed(DeviceGpsReadOutcomeEnum.Denied);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DeviceGpsReadModel.Failed(DeviceGpsReadOutcomeEnum.Timeout);
        }
    }

    private static Task<PermissionStatus> RequestAsync()
    {
        if (MainThread.IsMainThread)
        {
            return Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        return MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.LocationWhenInUse>);
    }

    private static DeviceLocationPermissionStatusEnum Map(PermissionStatus status)
    {
        return status switch
        {
            PermissionStatus.Granted or PermissionStatus.Limited => DeviceLocationPermissionStatusEnum.Granted,
            PermissionStatus.Denied => DeviceLocationPermissionStatusEnum.Denied,
            PermissionStatus.Restricted => DeviceLocationPermissionStatusEnum.PermanentlyDenied,
            _ => DeviceLocationPermissionStatusEnum.Unavailable
        };
    }

    private static double? AccuracyOrNull(double? accuracy)
    {
        return accuracy is > 0 && double.IsFinite(accuracy.Value) ? accuracy : null;
    }
}
