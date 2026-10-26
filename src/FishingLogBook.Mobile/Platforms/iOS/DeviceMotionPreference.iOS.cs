using UIKit;

namespace FishingLogBook.Mobile.Native.Services;

public sealed partial class DeviceMotionPreference
{
    private static partial bool QueryReduceMotion()
    {
        return UIAccessibility.IsReduceMotionEnabled;
    }
}
