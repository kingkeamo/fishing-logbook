using FishingLogBook.Mobile.Features.Settings.Services;

namespace FishingLogBook.Mobile.Native.Services;

public sealed class DeviceAppearanceService : IAppearanceService
{
    public void Apply(AppearanceEnum appearance)
    {
        Application.Current!.UserAppTheme = appearance switch
        {
            AppearanceEnum.Light => AppTheme.Light,
            AppearanceEnum.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}
