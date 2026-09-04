namespace FishingLogBook.Web.Features.Import.Enums;

public enum ImportTripSuggestionReasonEnum
{
    SameDate = 0,
    NearbyCoordinates = 1,
    ContinuousTime = 2,
    MissingGps = 3,
    ConsistentMethod = 4,
    ConsistentLocation = 5
}
