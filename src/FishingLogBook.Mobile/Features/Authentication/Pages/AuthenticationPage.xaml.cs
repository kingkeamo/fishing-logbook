using FishingLogBook.Mobile.Features.Authentication.ViewModels;

namespace FishingLogBook.Mobile.Features.Authentication.Pages;

public partial class AuthenticationPage : ContentPage
{
    private readonly AuthenticationViewModel _viewModel;

    public AuthenticationPage(AuthenticationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Attach(action => Dispatcher.Dispatch(action));
        await _viewModel.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        _viewModel.Detach();
        base.OnDisappearing();
    }

    private async void OnSignIn(object? sender, EventArgs args)
    {
        await _viewModel.SignInAsync();
    }

    private async void OnCancel(object? sender, EventArgs args)
    {
        await _viewModel.CancelAsync();
    }
}
