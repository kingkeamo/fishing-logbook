namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record TripAssociationModel(
    Guid TripId,
    string? Title,
    string? PlaceName,
    DateTimeOffset StartedOn,
    IReadOnlyList<TripAnglerOptionModel> Anglers);
