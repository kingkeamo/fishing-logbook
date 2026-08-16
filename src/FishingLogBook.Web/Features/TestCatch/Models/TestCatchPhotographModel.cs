using FishingLogBook.Web.Common;

namespace FishingLogBook.Web.Features.TestCatch.Models;

public sealed record TestCatchPhotographModel(
    Guid Id,
    string ContentType,
    SyncStatus SyncStatus,
    string? ObjectKey = null,
    string? RemoteUrl = null);
