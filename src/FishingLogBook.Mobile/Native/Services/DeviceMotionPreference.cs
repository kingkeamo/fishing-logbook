using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Native.Services;

public sealed partial class DeviceMotionPreference : IMotionPreference
{
    public bool ReduceMotion => QueryReduceMotion();

    private static partial bool QueryReduceMotion();
}
