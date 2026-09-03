namespace FishingLogBook.Web.Features.Import.Enums;

public enum ImportTimestampStateEnum
{
    Missing,
    Unusable,
    ExplicitInstant,
    LocalWallClock,
    WeakFallback,
    UserConfirmed
}
