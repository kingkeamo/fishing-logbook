using FishingLogBook.Mobile.Features.Dashboard.ViewModels;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Dashboard.Pages;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;
    private readonly NavigationService _navigation;
    private CancellationTokenSource? _appearance;

    public DashboardPage(DashboardViewModel viewModel, NavigationService navigation)
    {
        _viewModel = viewModel;
        _navigation = navigation;
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_appearance is not null)
        {
            return;
        }
        _appearance = new CancellationTokenSource();
        await _viewModel.LoadAsync(_appearance.Token);
    }

    protected override void OnDisappearing()
    {
        if (_viewModel.IsFilterModalOpen)
        {
            base.OnDisappearing();
            return;
        }
        _appearance?.Cancel();
        _appearance?.Dispose();
        _appearance = null;
        base.OnDisappearing();
    }

    private void OnOpenMenu(object? sender, EventArgs args)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    private async void OnRetry(object? sender, EventArgs args)
    {
        await _viewModel.LoadAsync(_appearance?.Token ?? CancellationToken.None);
    }

    private async void OnOpenCatch(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid id })
        {
            await _viewModel.OpenCatchAsync(id, _appearance?.Token ?? CancellationToken.None);
        }
    }

    private async void OnOpenFilters(object? sender, EventArgs args)
    {
        await _viewModel.EditFiltersAsync(_appearance?.Token ?? CancellationToken.None);
    }

    private async void OnOpenRow(object? sender, TappedEventArgs args)
    {
        if (args.Parameter is Guid id)
        {
            await _viewModel.OpenCatchAsync(id, _appearance?.Token ?? CancellationToken.None);
        }
    }

    private async void OnImport(object? sender, EventArgs args)
    {
        await _navigation.NavigateAsync(MobileRouteEnum.Import);
    }
}
