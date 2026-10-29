using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;

namespace FishingLogBook.Mobile;

public static partial class MauiProgram
{
    static partial void ConfigurePlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IDeviceUnlockService, IosDeviceUnlockService>();
    }

    private static partial MobileAuthConfig SelectPlatformCallbacks(MobileAuthConfig config)
    {
        return OperatingSystem.IsIOSVersionAtLeast(17, 4) ? config : config.WithSchemeCallbacks();
    }
}
