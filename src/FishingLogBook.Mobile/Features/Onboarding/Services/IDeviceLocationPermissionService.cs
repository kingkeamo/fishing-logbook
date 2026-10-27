using FishingLogBook.Mobile.Features.Onboarding.Enums;

namespace FishingLogBook.Mobile.Features.Onboarding.Services;

public interface IDeviceLocationPermissionService
{
    Task<LocationPermissionOutcomeEnum> RequestWhenInUseAsync(CancellationToken cancellationToken);
}
