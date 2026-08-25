namespace FishingLogBook.Web.Features.Catch.Models;

public sealed record PhotoMetadataModel(
    DateTimeOffset? CapturedOn,
    double? Latitude,
    double? Longitude)
{
    public static PhotoMetadataModel Empty { get; } = new(null, null, null);

    public bool HasCoordinates
    {
        get
        {
            return Latitude.HasValue && Longitude.HasValue;
        }
    }
}
