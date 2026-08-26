using FishingLogBook.Web.Features.Catch.Enums;

namespace FishingLogBook.Web.Features.Catch.Components.MeasurementEditor;

public sealed record MeasurementScaleRangeModel(
    MeasurementScaleRangeEnum Range,
    decimal MaximumCanonical)
{
    public static IReadOnlyList<MeasurementScaleRangeModel> Weights { get; } =
    [
        new(MeasurementScaleRangeEnum.Small, 5m),
        new(MeasurementScaleRangeEnum.Medium, 15m),
        new(MeasurementScaleRangeEnum.Large, 40m),
        new(MeasurementScaleRangeEnum.Monster, 120m)
    ];

    public static IReadOnlyList<MeasurementScaleRangeModel> Lengths { get; } =
    [
        new(MeasurementScaleRangeEnum.Small, 30m),
        new(MeasurementScaleRangeEnum.Medium, 60m),
        new(MeasurementScaleRangeEnum.Large, 120m),
        new(MeasurementScaleRangeEnum.Monster, 300m)
    ];

    public string LabelKey
    {
        get
        {
            return $"Catch_MeasurementRange_{Range}";
        }
    }

    public static IReadOnlyList<MeasurementScaleRangeModel> For(bool isWeight)
    {
        return isWeight ? Weights : Lengths;
    }

    public bool CanDisplay(decimal? canonicalValue)
    {
        return canonicalValue is null || canonicalValue.Value <= MaximumCanonical;
    }

    public static MeasurementScaleRangeModel SmallestFor(bool isWeight, decimal? canonicalValue)
    {
        var ranges = For(isWeight);
        return ranges.FirstOrDefault(range => range.CanDisplay(canonicalValue)) ?? ranges[^1];
    }

    public static MeasurementScaleRangeModel ExpandedFor(
        bool isWeight,
        MeasurementScaleRangeModel current,
        decimal? canonicalValue)
    {
        return current.CanDisplay(canonicalValue)
            ? current
            : SmallestFor(isWeight, canonicalValue);
    }
}
