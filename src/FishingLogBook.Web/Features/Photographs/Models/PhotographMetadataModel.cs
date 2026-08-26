using FishingLogBook.Web.Features.Photographs.Enums;

namespace FishingLogBook.Web.Features.Photographs.Models;

public sealed record PhotographMetadataModel(
    DateTimeOffset? CapturedOn,
    double? Latitude,
    double? Longitude,
    PhotographCapturedOnSourceEnum CapturedOnSource = PhotographCapturedOnSourceEnum.None)
{
    public static PhotographMetadataModel Empty { get; } = new(null, null, null);

    public bool HasCoordinates
    {
        get
        {
            return Latitude.HasValue && Longitude.HasValue;
        }
    }

    public bool HasTrustworthyCapturedOn
    {
        get
        {
            return CapturedOn.HasValue
                && CapturedOnSource is PhotographCapturedOnSourceEnum.ExifOriginal
                    or PhotographCapturedOnSourceEnum.ExifDigitized;
        }
    }

    public PhotographMetadataModel WithoutCapturedOn()
    {
        return this with { CapturedOn = null, CapturedOnSource = PhotographCapturedOnSourceEnum.None };
    }
}
