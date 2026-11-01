using System.Collections.ObjectModel;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Trips.ViewModels;

public sealed class StartPlannedTripViewModel(UiTextService text, TimeProvider clock)
{
    private IReadOnlyList<PlannedTripOptionModel> _options = [];

    public UiTextService Text { get; } = text;

    public ObservableCollection<StartPlannedTripItemModel> Items { get; } = [];

    public void Initialize(IReadOnlyList<PlannedTripOptionModel> options)
    {
        _options = options;
        Items.Clear();
        foreach (var option in options)
        {
            Items.Add(ToItem(option));
        }
    }

    public PlannedTripChoiceModel Choose(Guid tripId)
    {
        var selected = _options.FirstOrDefault(option => option.TripId == tripId);
        return selected is null ? PlannedTripChoiceModel.Dismissed : new PlannedTripChoiceModel(selected, IsDismissed: false);
    }

    public PlannedTripChoiceModel StartNew()
    {
        return PlannedTripChoiceModel.StartNew;
    }

    private StartPlannedTripItemModel ToItem(PlannedTripOptionModel option)
    {
        var when = TimeZoneInfo.ConvertTime(option.PlannedStartOn, clock.LocalTimeZone);
        var organiser = option.IsOrganiser
            ? Text["TripPlannedOrganiserYou"]
            : string.Format(Text.Culture, Text["TripPlannedOrganiser"], Name(option.OrganiserName));
        var guests = option.Guests.Count == 0
            ? string.Empty
            : string.Format(Text.Culture, Text["TripPlannedGuests"], string.Join(", ", option.Guests.Select(Guest)));
        return new StartPlannedTripItemModel(
            option.TripId,
            string.IsNullOrWhiteSpace(option.Title) ? Text["TripPlannedUntitled"] : option.Title,
            string.Format(Text.Culture, Text["TripPlannedFor"], when.ToString("g", Text.Culture)),
            organiser,
            guests,
            Text["TripStartThisTrip"]);
    }

    private string Guest(PlannedTripGuestModel guest)
    {
        var name = Name(guest.DisplayName);
        return guest.IsPending ? string.Format(Text.Culture, Text["TripPlannedGuestInvited"], name) : name;
    }

    private string Name(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? Text["TripContributorUnknown"] : name;
    }
}
