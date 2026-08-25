using FishingLogBook.Web.Features.Catch.Enums;

namespace FishingLogBook.Web.Features.Catch.Models;

public sealed record PhotoMetadataModel(
    DateTimeOffset? CapturedOn,
    double? Latitude,
    double? Longitude,
    PhotoCapturedOnSourceEnum CapturedOnSource = PhotoCapturedOnSourceEnum.None)
{
    public static PhotoMetadataModel Empty { get; } = new(null, null, null);

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
                && CapturedOnSource is PhotoCapturedOnSourceEnum.ExifOriginal
                    or PhotoCapturedOnSourceEnum.ExifDigitized;
        }
    }

    public PhotoMetadataModel WithoutCapturedOn()
    {
        return this with { CapturedOn = null, CapturedOnSource = PhotoCapturedOnSourceEnum.None };
    }
}
