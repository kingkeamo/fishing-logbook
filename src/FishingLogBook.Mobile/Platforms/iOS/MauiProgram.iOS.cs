using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using FishingLogBook.Mobile.Features.Photographs.Providers;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile;

public static partial class MauiProgram
{
    static partial void ConfigurePlatformLogging(ILoggingBuilder logging)
    {
        logging.AddProvider(new PhotographLoggerProvider(Console.WriteLine));
    }

    static partial void ConfigurePlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IDeviceUnlockService, IosDeviceUnlockService>();
    }

    private static partial MobileAuthConfig SelectPlatformCallbacks(MobileAuthConfig config)
    {
        return OperatingSystem.IsIOSVersionAtLeast(17, 4) ? config : config.WithSchemeCallbacks();
    }
}
