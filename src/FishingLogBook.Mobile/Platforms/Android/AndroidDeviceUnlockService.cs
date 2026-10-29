using FishingLogBook.Mobile.Features.OfflineAccess.Enums;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;

namespace FishingLogBook.Mobile;

public sealed class AndroidDeviceUnlockService : IDeviceUnlockService
{
    public Task<DeviceUnlockResultEnum> UnlockAsync(string reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Platform.CurrentActivity is MainActivity activity
            ? activity.VerifyDeviceOwnerAsync(reason, cancellationToken)
            : Task.FromResult(DeviceUnlockResultEnum.Unavailable);
    }
}
