using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Navigation;
using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Features.Catch.Pages;

public partial class CatchSummaryPage : ContentPage
{
    private readonly CatchListViewModel _viewModel;
    private readonly NavigationService _navigation;

    public CatchSummaryPage(CatchListViewModel viewModel, NavigationService navigation)
    {
        _viewModel = viewModel;
        _navigation = navigation;
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnRetry(object? sender, EventArgs args)
    {
        _viewModel.SetState(ContentStateEnum.Ready);
    }

    private async void OnOpenCatch(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: CatchPresentationModel item })
        {
            await _navigation.NavigateAsync(MobileRouteEnum.CatchDetail, item);
        }
    }
}
