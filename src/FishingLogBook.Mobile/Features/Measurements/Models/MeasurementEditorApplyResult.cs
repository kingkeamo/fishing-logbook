namespace FishingLogBook.Mobile.Features.Measurements.Models;

public sealed record MeasurementEditorApplyResult(bool Succeeded, decimal? CanonicalValue, string? ErrorMessage)
{
    public static MeasurementEditorApplyResult Applied(decimal? canonicalValue)
    {
        return new MeasurementEditorApplyResult(true, canonicalValue, null);
    }

    public static MeasurementEditorApplyResult Invalid(string errorMessage)
    {
        return new MeasurementEditorApplyResult(false, null, errorMessage);
    }
}
