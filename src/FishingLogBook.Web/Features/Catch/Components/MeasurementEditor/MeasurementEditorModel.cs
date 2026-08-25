using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Features.Catch.Components.MeasurementEditor;

public sealed record MeasurementEditorModel(
    bool IsWeight,
    decimal? CanonicalValue,
    WeightUnitEnum WeightUnit,
    LengthUnitEnum LengthUnit);
