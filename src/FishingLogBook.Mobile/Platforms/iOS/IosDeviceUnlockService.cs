using FishingLogBook.Mobile.Features.OfflineAccess.Enums;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using LocalAuthentication;

namespace FishingLogBook.Mobile;

public sealed class IosDeviceUnlockService : IDeviceUnlockService
{
    public async Task<DeviceUnlockResultEnum> UnlockAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var context = new LAContext();
        if (!context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthentication, out _))
        {
            return DeviceUnlockResultEnum.Unavailable;
        }

        try
        {
            using var registration = cancellationToken.Register(context.Invalidate);
            var result = await context.EvaluatePolicyAsync(LAPolicy.DeviceOwnerAuthentication, reason);
            cancellationToken.ThrowIfCancellationRequested();
            return result.Item1 ? DeviceUnlockResultEnum.Success : DeviceUnlockResultEnum.Failed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return cancellationToken.IsCancellationRequested
                ? DeviceUnlockResultEnum.Cancelled
                : DeviceUnlockResultEnum.Failed;
        }
    }
}
