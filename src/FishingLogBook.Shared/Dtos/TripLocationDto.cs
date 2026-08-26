namespace FishingLogBook.Shared.Dtos;

public sealed record TripLocationDto(
    double Latitude,
    double Longitude,
    double? AccuracyMetres,
    DateTimeOffset CapturedOn,
    string Source,
    string Visibility,
    string ConsentVersion);
