using FishingLogBook.Mobile.Features.About.ViewModels;

namespace FishingLogBook.Mobile.Features.About.Pages;

public partial class AboutPage : ContentPage
{
    private readonly AboutViewModel _viewModel;

    public AboutPage(AboutViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync(CancellationToken.None);
    }

    private async void OnRefresh(object? sender, EventArgs args)
    {
        await _viewModel.RefreshAsync(CancellationToken.None);
    }

    private async void OnCopy(object? sender, EventArgs args)
    {
        await _viewModel.CopyDiagnosticsAsync(CancellationToken.None);
    }
}
