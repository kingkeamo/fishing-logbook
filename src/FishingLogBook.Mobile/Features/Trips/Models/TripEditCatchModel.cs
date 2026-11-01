namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record TripEditCatchModel(Guid Id, string Label, string? Measurements, string RemoveLabel)
{
    public bool HasMeasurements => !string.IsNullOrWhiteSpace(Measurements);
}
