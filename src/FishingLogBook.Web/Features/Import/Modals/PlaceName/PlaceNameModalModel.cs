namespace FishingLogBook.Web.Features.Import.Modals.PlaceName;

public sealed record PlaceNameModalModel(
    IReadOnlyList<string> Components,
    string? CurrentPlaceName);
