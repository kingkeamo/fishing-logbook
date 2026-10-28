using FishingLogBook.Mobile.Features.Onboarding.Enums;
using FishingLogBook.Mobile.Features.Onboarding.Models;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.IO.Enums;
using FishingLogBook.Mobile.IO.Models;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Mobile.Features.Onboarding.Services;

public sealed class DeviceLocationService(IGeolocationAdapter geolocation) : IDeviceLocationService
{
    public const int CaptureTimeoutMilliseconds = 8000;

    public async Task<DeviceLocationCaptureModel> CaptureCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var permission = await EnsureWhenInUseAsync(cancellationToken);
        if (permission != DeviceLocationPermissionStatusEnum.Granted)
        {
            return DeviceLocationCaptureModel.WithoutCoordinates(MapPermission(permission));
        }

        try
        {
            var read = await geolocation.GetCurrentAsync(
                TimeSpan.FromMilliseconds(CaptureTimeoutMilliseconds),
                cancellationToken);
            return ToCapture(read);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DeviceLocationCaptureModel.WithoutCoordinates(LocationCaptureOutcomeEnum.Timeout);
        }
    }

    private async Task<DeviceLocationPermissionStatusEnum> EnsureWhenInUseAsync(
        CancellationToken cancellationToken)
    {
        var status = await geolocation.GetWhenInUseStatusAsync(cancellationToken);
        if (status == DeviceLocationPermissionStatusEnum.Granted)
        {
            return status;
        }

        return await geolocation.RequestWhenInUseAsync(cancellationToken);
    }

    private static DeviceLocationCaptureModel ToCapture(DeviceGpsReadModel read)
    {
        if (read.Outcome == DeviceGpsReadOutcomeEnum.Captured
            && read.Fix is DeviceGpsFixModel fix
            && CatchLocationConstants.AreCoordinatesValid(fix.Latitude, fix.Longitude))
        {
            return DeviceLocationCaptureModel.FromCoordinates(
                fix.Latitude,
                fix.Longitude,
                fix.AccuracyMetres,
                fix.CapturedOn);
        }

        return DeviceLocationCaptureModel.WithoutCoordinates(read.Outcome switch
        {
            DeviceGpsReadOutcomeEnum.Denied => LocationCaptureOutcomeEnum.Denied,
            DeviceGpsReadOutcomeEnum.Timeout => LocationCaptureOutcomeEnum.Timeout,
            _ => LocationCaptureOutcomeEnum.Unavailable
        });
    }

    private static LocationCaptureOutcomeEnum MapPermission(DeviceLocationPermissionStatusEnum status)
    {
        return status switch
        {
            DeviceLocationPermissionStatusEnum.Denied => LocationCaptureOutcomeEnum.Denied,
            DeviceLocationPermissionStatusEnum.PermanentlyDenied => LocationCaptureOutcomeEnum.PermanentlyDenied,
            _ => LocationCaptureOutcomeEnum.Unavailable
        };
    }
}
