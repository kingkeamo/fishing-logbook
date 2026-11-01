namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record PlannedTripLookupModel(IReadOnlyList<PlannedTripOptionModel> Options, bool Failed)
{
    public static PlannedTripLookupModel Succeeded(IReadOnlyList<PlannedTripOptionModel> options)
    {
        return new PlannedTripLookupModel(options, Failed: false);
    }

    public static PlannedTripLookupModel Unavailable(IReadOnlyList<PlannedTripOptionModel> localOptions)
    {
        return new PlannedTripLookupModel(localOptions, Failed: true);
    }
}
