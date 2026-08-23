namespace FishingLogBook.Web.Features.OfflineAccess.Models;

public sealed record OfflineAccessStatusModel(
    bool AccountEnabled,
    string DeviceState,
    OfflineAccessDeviceResultModel? Diagnostic = null)
{
    public bool IsReady => DeviceState == "ready";
}

public sealed record OfflineAccessDeviceResultModel(
    string State,
    string? Stage = null,
    string? ErrorName = null,
    string? ErrorMessage = null,
    bool? CreatePrfMatchesGet = null);

public sealed record OfflineAccessIdentityModel(Guid UserId, string Provider, string Subject);
