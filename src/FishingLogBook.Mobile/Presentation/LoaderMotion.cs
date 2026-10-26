namespace FishingLogBook.Mobile.Presentation;

public readonly record struct LoaderPose(double X, double Y, double Rotation, double Opacity, double Scale = 1d);

public static class LoaderMotion
{
    private const double LeapCycleMilliseconds = 1800d;
    private const double BoatCycleMilliseconds = 1600d;
    private const double BobberCycleMilliseconds = 2600d;
    private const double BaitVisibleUntil = 0.33d;
    private const double PropFlickersPerCycle = 9d;
    private const double WakeDashPeriod = 20d;
    private const double WakeDashesPerCycle = 2d;

    private static readonly (double At, LoaderPose Pose)[] Leap =
    [
        (0d, new LoaderPose(16, 72, -48, 0)),
        (0.12, new LoaderPose(24, 52, -28, 1)),
        (0.5, new LoaderPose(48, 20, 6, 1)),
        (0.88, new LoaderPose(72, 52, 32, 1)),
        (1d, new LoaderPose(80, 72, 48, 0))
    ];

    private static readonly (double At, LoaderPose Pose)[] SplashLeave =
    [
        (0d, new LoaderPose(0, 0, 0, 0, 0.4)),
        (0.08, new LoaderPose(0, 0, 0, 0, 0.4)),
        (0.16, new LoaderPose(0, 0, 0, 1, 1)),
        (0.32, new LoaderPose(0, 0, 0, 0, 1.5)),
        (1d, new LoaderPose(0, 0, 0, 0, 1.5))
    ];

    private static readonly (double At, LoaderPose Pose)[] SplashLand =
    [
        (0d, new LoaderPose(0, 0, 0, 0, 0.4)),
        (0.78, new LoaderPose(0, 0, 0, 0, 0.4)),
        (0.88, new LoaderPose(0, 0, 0, 1, 1)),
        (1d, new LoaderPose(0, 0, 0, 0, 1.5))
    ];

    private static readonly (double At, LoaderPose Pose)[] Yank =
    [
        (0d, new LoaderPose(0, 0, 0, 1)),
        (0.3, new LoaderPose(0, 0, 0, 1)),
        (0.38, new LoaderPose(0, 3, 4, 1)),
        (0.48, new LoaderPose(0, 16, 12, 1)),
        (0.6, new LoaderPose(0, 22, 8, 1)),
        (0.78, new LoaderPose(0, 5, -6, 1)),
        (1d, new LoaderPose(0, 0, 0, 1))
    ];

    private static readonly (double At, LoaderPose Pose)[] Take =
    [
        (0d, new LoaderPose(-6, 80, 0, 0)),
        (0.14, new LoaderPose(-6, 80, 0, 0)),
        (0.24, new LoaderPose(16, 82, 0, 1)),
        (0.34, new LoaderPose(36, 84, 0, 1)),
        (0.48, new LoaderPose(36, 84, 0, 1)),
        (0.58, new LoaderPose(58, 94, 0, 1)),
        (0.72, new LoaderPose(80, 108, 0, 0.35)),
        (0.84, new LoaderPose(102, 116, 0, 0)),
        (1d, new LoaderPose(102, 116, 0, 0))
    ];

    public static double StillPhase(LoaderKindEnum kind)
    {
        return kind == LoaderKindEnum.LeapingFish ? 0.5d : 0d;
    }

    public static uint CycleMilliseconds(LoaderKindEnum kind)
    {
        return (uint)(kind switch
        {
            LoaderKindEnum.LeapingFish => LeapCycleMilliseconds,
            LoaderKindEnum.Boat => BoatCycleMilliseconds,
            _ => BobberCycleMilliseconds
        });
    }

    public static double NormalisePhase(double phase)
    {
        var wrapped = phase % 1d;
        return wrapped < 0 ? wrapped + 1d : wrapped;
    }

    public static double EaseInOut(double progress)
    {
        var clamped = Math.Clamp(progress, 0d, 1d);
        return clamped * clamped * (3d - (2d * clamped));
    }

    public static LoaderPose Sample(IReadOnlyList<(double At, LoaderPose Pose)> frames, double phase)
    {
        var position = NormalisePhase(phase);
        for (var index = 1; index < frames.Count; index++)
        {
            var (end, to) = frames[index];
            if (position > end)
            {
                continue;
            }

            var (start, from) = frames[index - 1];
            var span = end - start;
            var amount = span <= 0d ? 1d : EaseInOut((position - start) / span);
            return new LoaderPose(
                Lerp(from.X, to.X, amount),
                Lerp(from.Y, to.Y, amount),
                Lerp(from.Rotation, to.Rotation, amount),
                Lerp(from.Opacity, to.Opacity, amount),
                Lerp(from.Scale, to.Scale, amount));
        }

        return frames[^1].Pose;
    }

    public static LoaderPose LeapingFish(double phase) => Sample(Leap, phase);

    public static LoaderPose LeapSplashLeave(double phase) => Sample(SplashLeave, phase);

    public static LoaderPose LeapSplashLand(double phase) => Sample(SplashLand, phase);

    public static LoaderPose BobberRig(double phase) => Sample(Yank, phase);

    public static LoaderPose BobberFish(double phase) => Sample(Take, phase);

    public static double WaveOffset(double phase, double delayCycles = 0d)
    {
        return -3d * Swing(phase + delayCycles);
    }

    public static bool IsBaitVisible(double phase)
    {
        return NormalisePhase(phase) < BaitVisibleUntil;
    }

    public static double BoatPitch(double phase)
    {
        return 10d - (4d * Swing(phase));
    }

    public static (double X, double Y) BoatDrift(double phase)
    {
        var swing = Swing(phase);
        return (-swing, 2d * swing);
    }

    public static double PropScale(double phase)
    {
        return 0.6d + (0.4d * Math.Cos(2d * Math.PI * PropFlickersPerCycle * NormalisePhase(phase)));
    }

    public static double WakeDashOffset(double phase)
    {
        return -WakeDashPeriod * WakeDashesPerCycle * NormalisePhase(phase);
    }

    private static double Swing(double phase)
    {
        return 0.5d - (0.5d * Math.Cos(2d * Math.PI * NormalisePhase(phase)));
    }

    private static double Lerp(double from, double to, double amount)
    {
        return from + ((to - from) * amount);
    }
}
