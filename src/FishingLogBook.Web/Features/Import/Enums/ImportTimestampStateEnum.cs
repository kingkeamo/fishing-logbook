namespace FishingLogBook.Web.Features.Import.Enums;

public enum ImportTimestampStateEnum
{
    Missing = 0,
    Unusable = 1,
    ExplicitInstant = 2,
    LocalWallClock = 3,
    WeakFallback = 4,
    UserConfirmed = 5
}
