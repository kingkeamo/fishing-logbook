namespace FishingLogBook.Web.Features.Catch.Models;

public sealed record PhotoMetadataProposalModel(
    DateTimeOffset? CaughtOn,
    double? Latitude,
    double? Longitude,
    DateTimeOffset? CoordinatesCapturedOn,
    bool HasConflictingDates,
    bool HasConflictingCoordinates)
{
    public static PhotoMetadataProposalModel Empty { get; } = new(null, null, null, null, false, false);

    public bool HasCoordinates
    {
        get
        {
            return Latitude.HasValue && Longitude.HasValue;
        }
    }
}
