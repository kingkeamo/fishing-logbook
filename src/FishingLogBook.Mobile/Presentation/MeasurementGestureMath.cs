namespace FishingLogBook.Mobile.Presentation;

public static class MeasurementGestureMath
{
    public const double TapeTrackStart = 62d;
    public const double TapeTrackLength = 166d;
    private const double WrapThreshold = 0.25d;

    public static double DialFraction(double dx, double dyUp, double currentFraction)
    {
        if (dx == 0d && dyUp == 0d)
        {
            return Math.Clamp(currentFraction, 0d, 1d);
        }

        var angle = Math.Atan2(dx, dyUp);
        var fraction = (angle < 0d ? angle + (2d * Math.PI) : angle) / (2d * Math.PI);
        if (currentFraction > 1d - WrapThreshold && fraction < WrapThreshold)
        {
            return 1d;
        }

        if (currentFraction < WrapThreshold && fraction > 1d - WrapThreshold)
        {
            return 0d;
        }

        return fraction;
    }

    public static double TapeFraction(double pointX, double offsetX, double scale)
    {
        var effectiveScale = scale <= 0d ? 1d : scale;
        var viewBoxX = (pointX - offsetX) / effectiveScale;
        return Math.Clamp((viewBoxX - TapeTrackStart) / TapeTrackLength, 0d, 1d);
    }
}
