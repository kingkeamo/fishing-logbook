using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Modals.AddTripCatches;

public sealed record AddTripCatchesModalModel(
    TripCatchScopeModel Scope,
    TripStorageEnum Storage = TripStorageEnum.LocalFirst,
    WeightUnitEnum WeightUnit = WeightUnitEnum.Kg,
    LengthUnitEnum LengthUnit = LengthUnitEnum.Cm);
