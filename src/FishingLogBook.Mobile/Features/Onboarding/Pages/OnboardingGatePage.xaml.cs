using FishingLogBook.Mobile.Features.Onboarding.ViewModels;

namespace FishingLogBook.Mobile.Features.Onboarding.Pages;

public partial class OnboardingGatePage : ContentPage
{
    private readonly OnboardingGateViewModel _viewModel;

    public OnboardingGatePage(OnboardingGateViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.StartAsync(CancellationToken.None);
    }

    private async void OnRetry(object? sender, EventArgs args)
    {
        await _viewModel.StartAsync(CancellationToken.None);
    }
}
