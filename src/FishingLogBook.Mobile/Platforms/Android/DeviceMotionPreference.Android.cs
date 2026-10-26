using Android.Provider;

namespace FishingLogBook.Mobile.Native.Services;

public sealed partial class DeviceMotionPreference
{
    private static partial bool QueryReduceMotion()
    {
        var resolver = global::Android.App.Application.Context.ContentResolver;
        return Settings.Global.GetFloat(resolver, Settings.Global.AnimatorDurationScale, 1f) == 0f;
    }
}
