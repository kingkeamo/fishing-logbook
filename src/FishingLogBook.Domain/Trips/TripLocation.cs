using FishingLogBook.Domain.Enums;

namespace FishingLogBook.Domain.Trips;

public sealed class TripLocation
{
    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public double? AccuracyMetres { get; init; }

    public DateTimeOffset CapturedOn { get; init; }

    public string Source { get; init; } = string.Empty;

    public string Visibility { get; init; } = string.Empty;

    public string ConsentVersion { get; init; } = string.Empty;

    public static TripLocation? TryCreate(
        double latitude,
        double longitude,
        double? accuracyMetres,
        DateTimeOffset capturedOn,
        string? source,
        string? visibility,
        string? consentVersion)
    {
        var trimmedVisibility = visibility?.Trim();
        if (!AreCoordinatesValid(latitude, longitude)
            || capturedOn == default
            || string.IsNullOrWhiteSpace(source)
            || string.IsNullOrWhiteSpace(trimmedVisibility)
            || !Enum.TryParse<LocationVisibilityEnum>(trimmedVisibility, ignoreCase: false, out _)
            || string.IsNullOrWhiteSpace(consentVersion))
        {
            return null;
        }

        return new TripLocation
        {
            Latitude = latitude,
            Longitude = longitude,
            AccuracyMetres = accuracyMetres,
            CapturedOn = capturedOn,
            Source = source.Trim(),
            Visibility = trimmedVisibility!,
            ConsentVersion = consentVersion.Trim()
        };
    }

    public static bool AreCoordinatesValid(double latitude, double longitude)
    {
        return latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
    }
}
