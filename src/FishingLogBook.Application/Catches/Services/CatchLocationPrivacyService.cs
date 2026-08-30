using FishingLogBook.Application.Catches.Contracts.Services;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.Catches.Services;

public sealed class CatchLocationPrivacyService : ICatchLocationPrivacyService
{
    public Task<CatchLocationExposureDto?> GetExposureAsync(
        Catch catchRecord,
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Shape(catchRecord, viewerUserId));
    }

    private static CatchLocationExposureDto? Shape(Catch catchRecord, Guid viewerUserId)
    {
        if (catchRecord.Location is null)
        {
            return null;
        }

        if (viewerUserId == catchRecord.UserId)
        {
            return Exact(catchRecord.Location);
        }

        if (!Enum.TryParse<LocationVisibilityEnum>(catchRecord.Location.Visibility, ignoreCase: false, out var visibility))
        {
            return Hidden(catchRecord.Location.Visibility);
        }

        return visibility switch
        {
            LocationVisibilityEnum.Private => Hidden(catchRecord.Location.Visibility),
            LocationVisibilityEnum.Approximate => Approximate(catchRecord.Location),
            LocationVisibilityEnum.FishingVenueOnly => VenueOnly(catchRecord.Location.Visibility),
            LocationVisibilityEnum.Public => Exact(catchRecord.Location),
            _ => Hidden(catchRecord.Location.Visibility)
        };
    }

    private static CatchLocationExposureDto Exact(CatchLocation location)
    {
        return new CatchLocationExposureDto
        {
            Visibility = location.Visibility,
            Mode = LocationDefaults.ExposureExact,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            AccuracyMetres = location.AccuracyMetres,
            CapturedOn = location.CapturedOn,
            Source = location.Source
        };
    }

    private static CatchLocationExposureDto Approximate(CatchLocation location)
    {
        return new CatchLocationExposureDto
        {
            Visibility = location.Visibility,
            Mode = LocationDefaults.ExposureApproximate,
            ApproximateLatitude = ClampLatitude(Quantize(location.Latitude)),
            ApproximateLongitude = WrapLongitude(Quantize(location.Longitude)),
            ApproximateCellSizeMetres = CatchLocationConstants.ApproximateCellSizeMetres
        };
    }

    private static CatchLocationExposureDto VenueOnly(string visibility)
    {
        return new CatchLocationExposureDto
        {
            Visibility = visibility,
            Mode = LocationDefaults.ExposureFishingVenue
        };
    }

    private static CatchLocationExposureDto Hidden(string visibility)
    {
        return new CatchLocationExposureDto
        {
            Visibility = visibility,
            Mode = LocationDefaults.ExposureNone
        };
    }

    private static double Quantize(double value)
    {
        var grid = CatchLocationConstants.ApproximateGridDegrees;
        return Math.Floor(value / grid) * grid + (grid / 2.0);
    }

    private static double ClampLatitude(double latitude)
    {
        return Math.Clamp(latitude, CatchLocationConstants.MinLatitude, CatchLocationConstants.MaxLatitude);
    }

    private static double WrapLongitude(double longitude)
    {
        if (longitude > CatchLocationConstants.MaxLongitude)
        {
            return longitude - 360;
        }

        if (longitude < CatchLocationConstants.MinLongitude)
        {
            return longitude + 360;
        }

        return longitude;
    }
}
