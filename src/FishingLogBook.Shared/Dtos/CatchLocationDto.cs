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

    public const string Public = "Public";

    public const string ConsentVersion = "1";
}
