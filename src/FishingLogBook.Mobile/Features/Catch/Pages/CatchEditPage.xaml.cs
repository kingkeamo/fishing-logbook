using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Catch.Pages;

public partial class CatchEditPage : ContentPage, IQueryAttributable
{
    private readonly CatchEditViewModel _viewModel;
    private readonly NavigationService _navigation;
    private Guid? _catchId;
    private Guid? _tripId;
    private bool _initialized;

    public CatchEditPage(CatchEditViewModel viewModel, NavigationService navigation)
    {
        _viewModel = viewModel;
        _navigation = navigation;
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(RouteConstants.TripIdParameter, out var trip) && trip is Guid tripId)
        {
            _tripId = tripId;
        }

        if (query.TryGetValue(RouteConstants.CatchEditIdParameter, out var value) && value is Guid catchId)
        {
            _catchId = catchId;
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
        Unloaded += OnUnloaded;
        HandlerChanging += OnHandlerChanging;
        await _viewModel.InitializeAsync(_catchId, CancellationToken.None, _tripId);
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _viewModel.OnDisappearingAsync(CancellationToken.None);
    }

    private void OnUnloaded(object? sender, EventArgs args)
    {
        if (Navigation.NavigationStack.Contains(this))
        {
            return;
        }

        ReleaseViewModel();
    }

    private void OnHandlerChanging(object? sender, HandlerChangingEventArgs args)
    {
        if (args.NewHandler is null)
        {
            ReleaseViewModel();
        }
    }

    private void ReleaseViewModel()
    {
        Unloaded -= OnUnloaded;
        HandlerChanging -= OnHandlerChanging;
        _viewModel.Dispose();
    }

    private async void OnBack(object? sender, EventArgs args)
    {
        await _navigation.GoBackAsync();
    }

    private async void OnSave(object? sender, EventArgs args)
    {
        if (!await _viewModel.SaveAsync(CancellationToken.None))
        {
            return;
        }

        if (_tripId is not null)
        {
            await _navigation.GoBackAsync();
            return;
        }

        await _navigation.NavigateAsync(MobileRouteEnum.Catches);
    }
}
