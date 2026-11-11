using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Settings.Services;
using FishingLogBook.Mobile.Features.Settings.ViewModels;

namespace FishingLogBook.Mobile.Features.Settings.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    private readonly IAuthenticationService _authenticationService;

    public SettingsPage(SettingsViewModel viewModel, IAuthenticationService authenticationService)
    {
        _viewModel = viewModel;
        _authenticationService = authenticationService;
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Refresh();
        var email = _authenticationService.CurrentSession?.Email ?? string.Empty;
        AccountEmail.Text = string.Format(
            _viewModel.Text.Culture, _viewModel.Text["SignedInAs"], email);
    }

    private void OnAppearanceClicked(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: AppearanceEnum appearance } && !_viewModel.SelectAppearance(appearance))
        {
            SemanticScreenReader.Default.Announce(_viewModel.StatusText!);
        }
    }

    private async void OnSignOut(object? sender, EventArgs args)
    {
        await _authenticationService.SignOutAsync(CancellationToken.None);
    }
}
