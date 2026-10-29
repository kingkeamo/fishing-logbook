using FishingLogBook.Mobile.Features.Startup.Coordinators;
using FishingLogBook.Mobile.Features.Startup.ViewModels;

namespace FishingLogBook.Mobile.Features.Startup.Pages;

public partial class StartupPage : ContentPage
{
    private readonly StartupViewModel _viewModel;
    private readonly StartupOperationCoordinator _operationCoordinator;

    public StartupPage(
        StartupViewModel viewModel,
        StartupOperationCoordinator operationCoordinator)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _operationCoordinator = operationCoordinator;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await StartAsync();
    }

    protected override void OnDisappearing()
    {
        _operationCoordinator.Cancel();
        base.OnDisappearing();
    }

    private async void OnRetry(object? sender, EventArgs args)
    {
        await StartAsync();
    }

    private async Task StartAsync()
    {
        await _operationCoordinator.RestartAsync(_viewModel.StartAsync);
    }
}
