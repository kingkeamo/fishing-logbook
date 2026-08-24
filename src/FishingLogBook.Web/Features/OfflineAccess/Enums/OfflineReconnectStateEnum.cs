namespace FishingLogBook.Web.Features.OfflineAccess.Enums;

public enum OfflineReconnectStateEnum
{
    Offline = 0,
    ConnectivityRestored = 1,
    RecoveringAuthentication = 2,
    AuthenticationRequired = 3,
    VerifyingOwner = 4,
    OwnerMismatch = 5,
    Synchronising = 6,
    Online = 7,
    RetryableFailure = 8
}
