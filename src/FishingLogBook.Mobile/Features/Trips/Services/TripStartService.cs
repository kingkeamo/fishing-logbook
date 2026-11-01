using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Models;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public sealed class TripStartService(ITripService trips, IPlannedTripService plannedTrips, IPlannedTripChooser chooser,
    IAnglerPreferencesService preferences) : ITripStartService
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
        var eligible = await ReadEligibleAsync(cancellationToken);
        return eligible.Count == 0 ? PlannedTripChoiceModel.StartNew : await chooser.ChoosePlannedTripAsync(eligible);
    }

    private async Task<IReadOnlyList<PlannedTripOptionModel>> ReadEligibleAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await plannedTrips.GetEligibleAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or IOException)
        {
            return [];
        }
    }
}
