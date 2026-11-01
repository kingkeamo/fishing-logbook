using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Trips.Pages;

public partial class TripsPage : ContentPage
{
    private readonly TripListViewModel _viewModel;
    private readonly NavigationService _navigation;

    public TripsPage(TripListViewModel viewModel, NavigationService navigation)
    {
        _viewModel = viewModel;
        _navigation = navigation;
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync(CancellationToken.None);
    }

    private void OnOpenMenu(object? sender, EventArgs args)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    private async void OnRetry(object? sender, EventArgs args)
    {
        await _viewModel.LoadAsync(CancellationToken.None);
    }

    private async void OnStart(object? sender, EventArgs args)
    {
        if (await _viewModel.StartAsync(CancellationToken.None) is { } tripId)
        {
            await _navigation.NavigateAsync(MobileRouteEnum.TripDetail, tripId: tripId);
        }
    }

    private async void OnOpenTrip(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid tripId })
        {
            await _navigation.NavigateAsync(_viewModel.RouteFor(tripId), tripId: tripId);
        }
    }

    private async void OnPlan(object? sender, EventArgs args)
    {
        await _navigation.NavigateAsync(MobileRouteEnum.TripPlan);
    }

    private async void OnAccept(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid tripId })
        {
            await _viewModel.AcceptAsync(tripId, CancellationToken.None);
        }
    }

    private async void OnDecline(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid tripId })
        {
            await _viewModel.DeclineAsync(tripId, CancellationToken.None);
        }
    }
}
