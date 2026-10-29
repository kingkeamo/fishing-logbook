using Android.Content.Res;
using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;

namespace FishingLogBook.Mobile;

public static partial class MauiProgram
{
    static partial void ConfigurePlatformLogging(ILoggingBuilder logging)
    {
        logging.AddProvider(new AndroidLogcatLoggerProvider());
    }

    private static partial MobileAuthConfig SelectPlatformCallbacks(MobileAuthConfig config)
    {
        return config;
    }

    static partial void ConfigurePlatformHandlers()
    {
        EntryHandler.Mapper.AppendToMapping("BorderlessEntry", static (handler, _) =>
        {
            handler.PlatformView.BackgroundTintList = ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        });
    }

    static partial void ConfigurePlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IDeviceUnlockService, AndroidDeviceUnlockService>();
    }
}
