using FishingLogBook.Mobile.Features.Onboarding.Enums;
using FishingLogBook.Mobile.Features.Onboarding.Services;

namespace FishingLogBook.Mobile.Native.Services;

public sealed class DeviceLocationPermissionService : IDeviceLocationPermissionService
{
    public async Task<LocationPermissionOutcomeEnum> RequestWhenInUseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (IsGranted(status))
            {
                return LocationPermissionOutcomeEnum.Granted;
            }

            status = await RequestAsync();
            return Map(status);
        }
        catch (FeatureNotSupportedException)
        {
            return LocationPermissionOutcomeEnum.Unavailable;
        }
        catch (PermissionException)
        {
            return LocationPermissionOutcomeEnum.Unavailable;
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

    private static LocationPermissionOutcomeEnum Map(PermissionStatus status)
    {
        if (IsGranted(status))
        {
            return LocationPermissionOutcomeEnum.Granted;
        }

        return status switch
        {
            PermissionStatus.Denied => LocationPermissionOutcomeEnum.Denied,
            PermissionStatus.Disabled => LocationPermissionOutcomeEnum.Unavailable,
            PermissionStatus.Restricted => LocationPermissionOutcomeEnum.PermanentlyDenied,
            _ => LocationPermissionOutcomeEnum.Unavailable
        };
    }

    private static bool IsGranted(PermissionStatus status)
    {
        return status is PermissionStatus.Granted or PermissionStatus.Limited;
    }
}
