namespace FishingLogBook.Mobile.Features.Authentication.Enums;

public enum SessionLifecycleStateEnum
{
    Idle = 0,
    LocalOffline = 1,
    Recovering = 2,
    Online = 3,
    RecoverableError = 4,
    SignInRequired = 5
}
