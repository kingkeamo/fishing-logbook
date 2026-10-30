using FishingLogBook.Shared.Photographs.Enums;

namespace FishingLogBook.Shared.Photographs.Models;

public sealed record PhotographHistoricalMetadataModel(
    DateTimeOffset? ExplicitInstant,
    DateTime? LocalWallClock,
    PhotographCapturedOnSourceEnum CapturedOnSource,
    bool CapturedOnWasPresent,
    bool CapturedOnWasMalformed,
    double? Latitude,
    double? Longitude)
{
    public bool HasCoordinates
    {
        get
        {
            return Latitude.HasValue && Longitude.HasValue;
        }
    }
}
