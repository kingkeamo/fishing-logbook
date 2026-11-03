namespace FishingLogBook.Mobile.Features.Import.Models;

public sealed record PlaceNameModalModel(
    IReadOnlyList<string> Components,
    string? CurrentPlaceName);
