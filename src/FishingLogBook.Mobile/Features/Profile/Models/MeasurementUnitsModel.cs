using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Mobile.Features.Profile.Models;

public sealed record MeasurementUnitsModel(WeightUnitEnum WeightUnit, LengthUnitEnum LengthUnit)
{
    public static MeasurementUnitsModel Default { get; } = new(WeightUnitEnum.Kg, LengthUnitEnum.Cm);

    public static MeasurementUnitsModel From(ProfileDto profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new MeasurementUnitsModel(profile.PreferredWeightUnit, profile.PreferredLengthUnit);
    }
}
