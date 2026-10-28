namespace FishingLogBook.Mobile.IO.Models;

public sealed record DeviceGpsFixModel(
    double Latitude,
    double Longitude,
    double? AccuracyMetres,
    DateTimeOffset CapturedOn);
