using System.Collections.ObjectModel;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Import.ViewModels;

public sealed class ExistingTripModalViewModel
{
    public ExistingTripModalViewModel(UiTextService text, IReadOnlyList<TripSummaryDto> trips)
    {
        Text = text;
        foreach (var trip in trips)
        {
            var title = string.IsNullOrWhiteSpace(trip.Title)
                ? trip.StartedOn.ToString("d", text.Culture)
                : trip.Title;
            var when = trip.EndedOn is { } ended
                ? string.Format(
                    text.Culture,
                    text["Import_TripTimeRange"],
                    trip.StartedOn.ToString("g", text.Culture),
                    ended.ToString("g", text.Culture))
                : trip.StartedOn.ToString("g", text.Culture);
            Items.Add(new ExistingTripItemModel(trip.Id, title, when, text["Import_AddToExistingTrip"]));
        }
    }

    public UiTextService Text { get; }

    public ObservableCollection<ExistingTripItemModel> Items { get; } = [];
}
