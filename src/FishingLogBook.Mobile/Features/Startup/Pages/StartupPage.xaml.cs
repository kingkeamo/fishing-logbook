using FishingLogBook.Mobile.Features.Startup.ViewModels;

namespace FishingLogBook.Mobile.Features.Startup.Pages;

public partial class StartupPage : ContentPage
{
    private readonly StartupViewModel _viewModel;
    private CancellationTokenSource? _startupCancellation;

    public StartupPage(StartupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await StartAsync();
    }

    protected override void OnDisappearing()
    {
        _startupCancellation?.Cancel();
        _startupCancellation?.Dispose();
        _startupCancellation = null;
        base.OnDisappearing();
    }

    private async void OnRetry(object? sender, EventArgs args)
    {
        await StartAsync();
    }

    private async Task StartAsync()
    {
        _startupCancellation?.Cancel();
        _startupCancellation?.Dispose();
        _startupCancellation = new CancellationTokenSource();
        await _viewModel.StartAsync(_startupCancellation.Token);
    }
}
