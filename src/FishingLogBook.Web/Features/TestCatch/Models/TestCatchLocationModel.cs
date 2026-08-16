namespace FishingLogBook.Web.Features.TestCatch.Models;

public sealed record TestCatchLocationModel(
    double Latitude,
    double Longitude,
    double? AccuracyMetres,
    DateTimeOffset CapturedOn,
    string Source,
    string Visibility,
    string ConsentVersion);
