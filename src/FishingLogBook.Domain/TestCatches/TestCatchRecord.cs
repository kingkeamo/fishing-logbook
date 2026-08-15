namespace FishingLogBook.Domain.TestCatches;

public sealed class TestCatchRecord
{
    public Guid Id { get; init; }

    public string SpeciesName { get; init; } = string.Empty;

    public DateTimeOffset CaughtOn { get; init; }

    public string? Notes { get; init; }

    public Guid? PhotographId { get; init; }

    public string? PhotographObjectKey { get; init; }

    public string? PhotographContentType { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public double? LocationAccuracyMetres { get; init; }

    public DateTimeOffset? LocationCapturedOn { get; init; }

    public string? LocationSource { get; init; }

    public string? LocationVisibility { get; init; }

    public string? LocationConsentVersion { get; init; }
}
