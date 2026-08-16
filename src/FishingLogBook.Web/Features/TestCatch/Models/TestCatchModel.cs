using FishingLogBook.Web.Common;

namespace FishingLogBook.Web.Features.TestCatch.Models;

public sealed record TestCatchModel(
    Guid Id,
    string SpeciesName,
    DateTimeOffset CaughtOn,
    string? Notes,
    SyncStatus SyncStatus,
    TestCatchPhotographModel? Photograph = null,
    TestCatchLocationModel? Location = null);
