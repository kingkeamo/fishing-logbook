using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Trips.Pages;

public partial class TripPlanPage : ContentPage, IQueryAttributable
{
    private readonly TripPlanViewModel _viewModel;
    private readonly NavigationService _navigation;
    private Guid? _tripId;
    private bool _initialized;

    public TripPlanPage(TripPlanViewModel viewModel, NavigationService navigation, TripParticipantsViewModel participants)
    {
        _viewModel = viewModel;
        _navigation = navigation;
        InitializeComponent();
        BindingContext = viewModel;
        Participants.ViewModel = participants;
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
        if (_initialized)
        {
            return;
        }
        _initialized = true;
        await _viewModel.LoadAsync(_tripId, CancellationToken.None);
        Participants.ViewModel!.AllowManagement = _viewModel.CanCancelTrip;
        await Participants.LoadAsync(_viewModel.TripId ?? Guid.Empty);
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

}
