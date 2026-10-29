using FishingLogBook.Mobile.Features.OfflineAccess.Enums;

namespace FishingLogBook.Mobile.Features.OfflineAccess.Services;

public interface IDeviceUnlockService
{
    Task<DeviceUnlockResultEnum> UnlockAsync(string reason, CancellationToken cancellationToken);
}
