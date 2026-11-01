using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public sealed class TripStartService(ITripService trips, IPlannedTripService plannedTrips, IPlannedTripChooser chooser,
    IAnglerPreferencesService preferences, InteractionService interactions, UiTextService text) : ITripStartService
{
    public async Task<Guid?> StartAsync(CancellationToken cancellationToken)
    {
        var choice = await ChooseAsync(cancellationToken);
        if (choice.IsDismissed)
        {
            return null;
        }

        if (choice.Selected is { } selected)
        {
            return await plannedTrips.ActivateAsync(selected, cancellationToken);
        }

        var cached = await preferences.GetCachedAsync(cancellationToken);
        var place = cached?.Locations.Locations.FirstOrDefault(location => location.IsDefault)?.Name;
        var trip = await trips.StartAsync(null, place, cancellationToken);
        return trip.Detail.Trip.Id;
    }

    private async Task<PlannedTripChoiceModel> ChooseAsync(CancellationToken cancellationToken)
    {
        var lookup = await plannedTrips.GetEligibleAsync(cancellationToken);
        while (lookup.Failed)
        {
            if (!await interactions.ConfirmAsync(new DialogRequest(
                    text["TripPlannedLookupFailedTitle"], text["TripPlannedLookupFailed"], text["Retry"], text["Cancel"]), cancellationToken))
            {
                return PlannedTripChoiceModel.Dismissed;
            }

            lookup = await plannedTrips.GetEligibleAsync(cancellationToken);
        }

        return lookup.Options.Count == 0 ? PlannedTripChoiceModel.StartNew : await chooser.ChoosePlannedTripAsync(lookup.Options);
    }
}
