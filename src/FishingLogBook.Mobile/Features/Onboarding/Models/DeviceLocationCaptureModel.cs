using FishingLogBook.Mobile.Features.Onboarding.Enums;

namespace FishingLogBook.Mobile.Features.Onboarding.Models;

public sealed record DeviceLocationCaptureModel(
    LocationCaptureOutcomeEnum Outcome,
    double? Latitude,
    double? Longitude,
    double? AccuracyMetres,
    DateTimeOffset? CapturedOn)
{
    public static DeviceLocationCaptureModel FromCoordinates(
        double latitude,
        double longitude,
        double? accuracyMetres,
        DateTimeOffset capturedOn)
    {
        return new(
            LocationCaptureOutcomeEnum.Captured,
            latitude,
            longitude,
            accuracyMetres,
            capturedOn);
    }

    public static DeviceLocationCaptureModel WithoutCoordinates(LocationCaptureOutcomeEnum outcome)
    {
        return new(outcome, null, null, null, null);
    }
}
