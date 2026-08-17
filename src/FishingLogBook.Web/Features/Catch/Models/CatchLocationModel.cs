namespace FishingLogBook.Web.Features.Catch.Models;

public sealed record CatchLocationModel(
    double Latitude,
    double Longitude,
    double? AccuracyMetres,
    DateTimeOffset CapturedOn,
    string Source,
    string Visibility,
    string ConsentVersion);
