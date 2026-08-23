namespace FishingLogBook.Web.Features.OfflineAccess.Models;

public sealed record OfflineAccessStatusModel(bool AccountEnabled, string DeviceState)
{
    public bool IsReady => DeviceState == "ready";
}

public sealed record OfflineAccessDeviceResultModel(string State);

public sealed record OfflineAccessIdentityModel(Guid UserId, string Provider, string Subject);
