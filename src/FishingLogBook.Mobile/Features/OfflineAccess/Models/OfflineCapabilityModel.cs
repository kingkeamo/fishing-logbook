namespace FishingLogBook.Mobile.Features.OfflineAccess.Models;

public sealed record OfflineCapabilityModel(
    string Subject,
    Guid UserId,
    bool IsDeviceReady);
