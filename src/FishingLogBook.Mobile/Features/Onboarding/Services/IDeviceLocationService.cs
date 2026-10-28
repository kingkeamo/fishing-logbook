using FishingLogBook.Mobile.Features.Onboarding.Models;

namespace FishingLogBook.Mobile.Features.Onboarding.Services;

public interface IDeviceLocationService
{
    Task<DeviceLocationCaptureModel> CaptureCurrentAsync(CancellationToken cancellationToken);
}
