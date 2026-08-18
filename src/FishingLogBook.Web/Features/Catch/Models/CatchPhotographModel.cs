using FishingLogBook.Web.Common;

namespace FishingLogBook.Web.Features.Catch.Models;

public sealed record CatchPhotographModel(
    Guid Id,
    Guid CatchId,
    string ContentType,
    byte[]? Bytes = null,
    SyncStatus SyncStatus = SyncStatus.SavedLocally,
    string? ObjectKey = null);
