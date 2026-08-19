using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Features.Catch.Services;

public interface IMeasurementService
{
    decimal? ToDisplayWeight(decimal? canonicalKilograms, WeightUnitEnum unit);

    decimal? ToCanonicalWeight(decimal? displayValue, WeightUnitEnum unit, decimal? existingCanonicalKilograms);

    decimal? ToDisplayLength(decimal? canonicalCentimetres, LengthUnitEnum unit);

    decimal? ToCanonicalLength(decimal? displayValue, LengthUnitEnum unit, decimal? existingCanonicalCentimetres);

    decimal MaxDisplayWeight(WeightUnitEnum unit);

    decimal MaxDisplayLength(LengthUnitEnum unit);
}
