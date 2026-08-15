using FishingLogBook.Web.Models;

namespace FishingLogBook.Web.Offline;

public sealed record TestCatch(
    Guid Id,
    string SpeciesName,
    DateTimeOffset CaughtOn,
    string? Notes,
    SyncStatus SyncStatus,
    TestCatchPhotograph? Photograph = null,
    TestCatchLocation? Location = null);
