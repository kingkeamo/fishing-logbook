using FishingLogBook.Web.Models;

namespace FishingLogBook.Web.Offline;

public sealed record TestCatchPhotograph(
    Guid Id,
    string ContentType,
    SyncStatus SyncStatus,
    string? ObjectKey = null,
    string? RemoteUrl = null);
