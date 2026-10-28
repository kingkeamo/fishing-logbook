using FishingLogBook.Mobile.Features.Onboarding.ViewModels;

namespace FishingLogBook.Mobile.Features.Onboarding.Pages;

public partial class OnboardingPage : ContentPage
{
    private readonly OnboardingViewModel _viewModel;

    public OnboardingPage(OnboardingViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel.Profile.Attach(action => Dispatcher.Dispatch(action));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.StartAsync(CancellationToken.None);
    }

    private async void OnRetryLoad(object? sender, EventArgs args)
    {
        await _viewModel.StartAsync(CancellationToken.None);
    }

    private void OnBack(object? sender, EventArgs args)
    {
        _viewModel.Back();
    }

    private async void OnContinue(object? sender, EventArgs args)
    {
        await _viewModel.ContinueAsync(CancellationToken.None);
    }

    private async void OnAllowLocation(object? sender, EventArgs args)
    {
        await _viewModel.AllowLocationAsync(CancellationToken.None);
    }

    private void OnSkipLocation(object? sender, EventArgs args)
    {
        _viewModel.SkipLocation();
    }
}
