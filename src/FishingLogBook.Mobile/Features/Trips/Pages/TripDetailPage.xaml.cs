using FishingLogBook.Mobile.Features.Trips.Modals;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Trips.Pages;

/// <summary>The trip screen. It loads the trip, gives the components their context, and handles navigation and refresh.</summary>
public partial class TripDetailPage : ContentPage, IQueryAttributable
{
    private readonly TripDetailViewModel _viewModel;
    private readonly NavigationService _navigation;
    private Guid _tripId;

    public TripDetailPage(TripDetailViewModel viewModel, NavigationService navigation, ITripModalService modals)
    {
        _viewModel = viewModel;
        _navigation = navigation;
        InitializeComponent();
        BindingContext = viewModel;
        ActiveTrip.Modals = modals;
        NoteTrigger.Modals = modals;
        ActiveTrip.Changed += OnChanged;
        NoteTrigger.Changed += OnChanged;
        ActiveTrip.CatchOpenRequested += async (_, id) => await _navigation.NavigateAsync(MobileRouteEnum.CatchEdit, catchEditId: id);
        ActiveTrip.RecordCatchRequested += async (_, _) => await _navigation.NavigateAsync(MobileRouteEnum.CatchEdit, tripId: _tripId);
        ActiveTrip.UpdateRequested += async (_, _) => await _navigation.NavigateAsync(MobileRouteEnum.TripEdit, tripId: _tripId);
        ActiveTrip.LogbookRequested += async (_, _) => await _navigation.NavigateAsync(MobileRouteEnum.Catches);
        ActiveTrip.FinishRequested += async (_, _) => await _viewModel.FinishAsync(CancellationToken.None);
        ActiveTrip.PhotoToggleRequested += (_, _) => _viewModel.TogglePhotographPicker();
        ActiveTrip.TakePhotoRequested += async (_, _) => await _viewModel.AddPhotographAsync(useCamera: true, CancellationToken.None);
        ActiveTrip.ChoosePhotoRequested += async (_, _) => await _viewModel.AddPhotographAsync(useCamera: false, CancellationToken.None);
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
        if (_tripId != Guid.Empty)
        {
            await _viewModel.LoadAsync(_tripId, CancellationToken.None);
            if (_viewModel.IsPlannedLifecycle)
            {
                await _navigation.NavigateAsync(MobileRouteEnum.TripPlan, tripId: _tripId);
            }
        }
    }

    private async void OnBack(object? sender, EventArgs args)
    {
        await _navigation.GoBackAsync();
    }

    private async void OnChanged(object? sender, EventArgs args)
    {
        await _viewModel.ReloadAsync(CancellationToken.None);
    }
}
