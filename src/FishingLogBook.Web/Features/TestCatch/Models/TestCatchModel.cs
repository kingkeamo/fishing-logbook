using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.TestCatch.Models;

public sealed record TestCatchModel(
    Guid Id,
    string SpeciesName,
    DateTimeOffset CaughtOn,
    string? Notes,
    SyncStatus SyncStatus,
    TestCatchPhotographModel? Photograph = null,
    CatchLocationModel? Location = null);
