namespace FishingLogBook.Web.Features.Trips.Models;

public sealed record TripLocationModel(
    double Latitude,
    double Longitude,
    double? AccuracyMetres,
    DateTimeOffset CapturedOn,
    string Source,
    string Visibility,
    string ConsentVersion);
