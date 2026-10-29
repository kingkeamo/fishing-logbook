namespace FishingLogBook.Mobile.Features.Startup.Enums;

public enum StartupResultEnum
{
    SignInRequired = 0,
    OnlineSessionReady = 1,
    OfflineSessionReady = 2,
    OfflineAccessUnavailable = 3,
    UnlockCancelled = 4,
    UnlockFailed = 5,
    ServiceUnavailable = 6
}
