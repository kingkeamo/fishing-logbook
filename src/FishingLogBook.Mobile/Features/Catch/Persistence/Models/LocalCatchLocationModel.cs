namespace FishingLogBook.Mobile.Features.Catch.Persistence.Models;

public sealed class LocalCatchLocationModel
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    public double? AccuracyMetres { get; init; }

    public required DateTimeOffset CapturedOn { get; init; }

    public required string Source { get; init; }

    public required string Visibility { get; init; }

    public required string ConsentVersion { get; init; }
}
