using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Features.Catch.Services;

public sealed class MeasurementService : IMeasurementService
{
    private const decimal KilogramsPerPound = 0.45359237m;
    private const decimal CentimetresPerInch = 2.54m;
    private const int CanonicalWeightDecimals = 3;
    private const int CanonicalLengthDecimals = 2;
    private const int ImperialDecimals = 2;

    public decimal? ToDisplayWeight(decimal? canonicalKilograms, WeightUnitEnum unit)
    {
        if (canonicalKilograms is null)
        {
            return null;
        }

        if (unit != WeightUnitEnum.Lb)
        {
            return canonicalKilograms;
        }

        return Round(canonicalKilograms.Value / KilogramsPerPound, ImperialDecimals);
    }

    public decimal? ToCanonicalWeight(
        decimal? displayValue,
        WeightUnitEnum unit,
        decimal? existingCanonicalKilograms)
    {
        if (displayValue is null)
        {
            return null;
        }

        if (ToDisplayWeight(existingCanonicalKilograms, unit) == displayValue)
        {
            return existingCanonicalKilograms;
        }

        var kilograms = unit == WeightUnitEnum.Lb
            ? displayValue.Value * KilogramsPerPound
            : displayValue.Value;
        return Round(kilograms, CanonicalWeightDecimals);
    }

    public decimal? ToDisplayLength(decimal? canonicalCentimetres, LengthUnitEnum unit)
    {
        if (canonicalCentimetres is null)
        {
            return null;
        }

        if (unit != LengthUnitEnum.In)
        {
            return canonicalCentimetres;
        }

        return Round(canonicalCentimetres.Value / CentimetresPerInch, ImperialDecimals);
    }

    public decimal? ToCanonicalLength(
        decimal? displayValue,
        LengthUnitEnum unit,
        decimal? existingCanonicalCentimetres)
    {
        if (displayValue is null)
        {
            return null;
        }

        if (ToDisplayLength(existingCanonicalCentimetres, unit) == displayValue)
        {
            return existingCanonicalCentimetres;
        }

        var centimetres = unit == LengthUnitEnum.In
            ? displayValue.Value * CentimetresPerInch
            : displayValue.Value;
        return Round(centimetres, CanonicalLengthDecimals);
    }

    public decimal MaxDisplayWeight(WeightUnitEnum unit)
    {
        return unit == WeightUnitEnum.Lb
            ? Round(CatchDetailConstants.MaxWeightKilograms / KilogramsPerPound, ImperialDecimals)
            : CatchDetailConstants.MaxWeightKilograms;
    }

    public decimal MaxDisplayLength(LengthUnitEnum unit)
    {
        return unit == LengthUnitEnum.In
            ? Round(CatchDetailConstants.MaxLengthCentimetres / CentimetresPerInch, ImperialDecimals)
            : CatchDetailConstants.MaxLengthCentimetres;
    }

    private static decimal Round(decimal value, int decimals)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }
}
