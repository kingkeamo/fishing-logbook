namespace FishingLogBook.Shared.Constants;

public static class CatchLocationConstants
{
    public const double MinLatitude = -90;

    public const double MaxLatitude = 90;

    public const double MinLongitude = -180;

    public const double MaxLongitude = 180;

    public const double ApproximateGridDegrees = 0.05;

    public const double ApproximateCellSizeMetres = 5566;

    public static bool AreCoordinatesValid(double latitude, double longitude)
    {
        return latitude is >= MinLatitude and <= MaxLatitude
            && longitude is >= MinLongitude and <= MaxLongitude;
    }
}
