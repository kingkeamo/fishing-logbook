using FishingLogBook.Mobile.IO.Enums;
using FishingLogBook.Mobile.IO.Models;

namespace FishingLogBook.Mobile.IO.Adapters;

public interface IGeolocationAdapter
{
    Task<DeviceLocationPermissionStatusEnum> GetWhenInUseStatusAsync(CancellationToken cancellationToken);

    Task<DeviceLocationPermissionStatusEnum> RequestWhenInUseAsync(CancellationToken cancellationToken);

    Task<DeviceGpsReadModel> GetCurrentAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
