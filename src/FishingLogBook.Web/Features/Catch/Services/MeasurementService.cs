using System.Globalization;
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

    public string FormatWeight(
        decimal? canonicalKilograms,
        WeightUnitEnum unit,
        string unitLabel,
        string ounceUnitLabel)
    {
        if (canonicalKilograms is null)
        {
            return string.Empty;
        }

        if (unit == WeightUnitEnum.Lb)
        {
            var (pounds, ounces) = ToPoundsAndOunces(canonicalKilograms);
            return $"{pounds} {unitLabel} {ounces} {ounceUnitLabel}";
        }

        return $"{ToDisplayWeight(canonicalKilograms, unit):0.##} {unitLabel}";
    }

    public string FormatLength(decimal? canonicalCentimetres, LengthUnitEnum unit, string unitLabel)
    {
        if (canonicalCentimetres is null)
        {
            return string.Empty;
        }

        return $"{ToDisplayLength(canonicalCentimetres, unit)?.ToString("0.##", CultureInfo.CurrentCulture)} {unitLabel}";
    }

    public (int Pounds, int Ounces) ToPoundsAndOunces(decimal? canonicalKilograms)
    {
        if (canonicalKilograms is null)
        {
            return (0, 0);
        }

        var totalOunces = (int)Math.Round(
            canonicalKilograms.Value / KilogramsPerPound * 16m,
            MidpointRounding.AwayFromZero);
        return (totalOunces / 16, totalOunces % 16);
    }

    public decimal? FromPoundsAndOunces(int pounds, int ounces)
    {
        if (pounds < 0 || ounces < 0)
        {
            return null;
        }

        var totalOunces = (pounds * 16m) + ounces;
        if (totalOunces <= 0)
        {
            return null;
        }

        return Round(totalOunces / 16m * KilogramsPerPound, CanonicalWeightDecimals);
    }

    private static decimal Round(decimal value, int decimals)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }
}
