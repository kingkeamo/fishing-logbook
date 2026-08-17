namespace FishingLogBook.Web.Features.Catch.Models;

public sealed record CatchModel(
    Guid Id,
    DateTimeOffset CaughtOn,
    IReadOnlyList<CatchPhotographModel> Photographs,
    string? SpeciesName = null,
    CatchLocationModel? Location = null,
    Guid UserId = default);
