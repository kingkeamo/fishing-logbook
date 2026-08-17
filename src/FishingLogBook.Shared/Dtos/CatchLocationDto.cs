namespace FishingLogBook.Shared.Dtos;

public sealed record CatchLocationDto(
    double Latitude,
    double Longitude,
    double? AccuracyMetres,
    DateTimeOffset CapturedOn,
    string Source,
    string Visibility,
    string ConsentVersion);

public static class LocationDefaults
{
    public const string DeviceGps = "DeviceGps";

    public const string Private = "Private";

    public const string Approximate = "Approximate";

    public const string FishingVenueOnly = "FishingVenueOnly";

    public const string Public = "Public";

    public const string ExposureNone = "None";

    public const string ExposureExact = "Exact";

    public const string ExposureApproximate = "Approximate";

    public const string ExposureFishingVenue = "FishingVenue";

    public const string ConsentVersion = "1";

    public static bool IsKnownVisibility(string? visibility)
    {
        return visibility is Private or Approximate or FishingVenueOnly or Public;
    }
}
