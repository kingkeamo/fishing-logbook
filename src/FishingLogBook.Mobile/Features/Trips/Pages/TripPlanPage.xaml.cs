using FishingLogBook.Mobile.Features.Trips.Modals;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Trips.Pages;

public partial class TripPlanPage : ContentPage, IQueryAttributable
{
    private readonly TripPlanViewModel _viewModel;
    private readonly NavigationService _navigation;
    private readonly ITripModalService _modals;
    private Guid? _tripId;

    public TripPlanPage(TripPlanViewModel viewModel, NavigationService navigation, ITripModalService modals)
    {
        _viewModel = viewModel;
        _navigation = navigation;
        _modals = modals;
        InitializeComponent();
        BindingContext = viewModel;
        Location.LocationSelected += (_, name) => _viewModel.SelectLocation(name);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(RouteConstants.TripIdParameter, out var value) && value is Guid tripId)
        {
            _tripId = tripId;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync(_tripId, CancellationToken.None);
    }

    private async void OnBack(object? sender, EventArgs args)
    {
        await _navigation.GoBackAsync();
    }

    private async void OnSave(object? sender, EventArgs args)
    {
        if (await _viewModel.SaveAsync(CancellationToken.None))
        {
            await _navigation.GoBackAsync();
        }
    }

    private async void OnCancelTrip(object? sender, EventArgs args)
    {
        if (await _viewModel.CancelTripAsync(CancellationToken.None))
        {
            await _navigation.GoBackAsync();
        }
    }

    private async void OnInvite(object? sender, EventArgs args)
    {
        if (_viewModel.TripId is { } tripId)
        {
            await _modals.ShowParticipantsAsync(tripId);
        }
    }
}
