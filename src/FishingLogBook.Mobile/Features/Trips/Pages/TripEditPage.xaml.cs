using FishingLogBook.Mobile.Features.Trips.Modals;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Trips.Pages;

/// <summary>The Update trip screen. It loads the trip for the editor and handles saving, cancelling and refresh.</summary>
public partial class TripEditPage : ContentPage, IQueryAttributable
{
    private readonly TripEditViewModel _viewModel;
    private readonly NavigationService _navigation;
    private Guid _tripId;
    private bool _initialized;

    public TripEditPage(TripEditViewModel viewModel, NavigationService navigation, ITripModalService modals, TripParticipantsViewModel participants)
    {
        _viewModel = viewModel;
        _navigation = navigation;
        InitializeComponent();
        BindingContext = viewModel;
        Editor.ParticipantsSection.ViewModel = participants;
        Editor.Modals = modals;
        Editor.Changed += async (_, _) => await _viewModel.ReloadAsync(CancellationToken.None);
        Editor.RecordCatchRequested += async (_, _) => await _navigation.NavigateAsync(MobileRouteEnum.CatchEdit, tripId: _tripId);
        Editor.CancelRequested += async (_, _) => await _navigation.GoBackAsync();
        Editor.SaveRequested += async (_, _) =>
        {
            if (await _viewModel.SaveAsync(CancellationToken.None))
            {
                await _navigation.GoBackAsync();
            }
        };
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
        if (_tripId != Guid.Empty && !_initialized)
        {
            _initialized = true;
            await _viewModel.LoadAsync(_tripId, CancellationToken.None);
            await Editor.ParticipantsSection.LoadAsync(_tripId);
        }
    }

    private async void OnBack(object? sender, EventArgs args)
    {
        await _navigation.GoBackAsync();
    }
}
