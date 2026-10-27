using FishingLogBook.Mobile.Configuration;

namespace FishingLogBook.Mobile;

public static partial class MauiProgram
{
    private static partial MobileAuthConfig SelectPlatformCallbacks(MobileAuthConfig config)
    {
        return OperatingSystem.IsIOSVersionAtLeast(17, 4) ? config : config.WithSchemeCallbacks();
    }
}
