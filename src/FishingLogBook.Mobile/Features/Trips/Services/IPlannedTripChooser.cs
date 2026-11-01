using FishingLogBook.Mobile.Features.Trips.Models;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public interface IPlannedTripChooser
{
    Task<PlannedTripChoiceModel> ChoosePlannedTripAsync(IReadOnlyList<PlannedTripOptionModel> options);
}
