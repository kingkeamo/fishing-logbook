using FishingLogBook.Mobile.Features.Settings.Services;

namespace FishingLogBook.Mobile.Native.Services;

public sealed class DevicePreferenceStore : IPreferenceStore
{
    public string Get(string key, string fallback)
    {
        return Preferences.Default.Get(key, fallback);
    }

    public void Set(string key, string value)
    {
        Preferences.Default.Set(key, value);
    }
}
