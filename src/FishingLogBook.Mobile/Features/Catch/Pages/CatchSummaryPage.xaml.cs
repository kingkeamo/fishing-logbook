using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Navigation;
using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Features.Catch.Pages;

public partial class CatchSummaryPage : ContentPage
{
    private readonly CatchListViewModel _viewModel;
    private readonly NavigationService _navigation;
    private readonly InteractionService _interactions;

    public CatchSummaryPage(CatchListViewModel viewModel, NavigationService navigation, InteractionService interactions)
    {
        _viewModel = viewModel;
        _navigation = navigation;
        _interactions = interactions;
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs args)
    {
        if (_viewModel.SearchTerm != args.NewTextValue)
        {
            _viewModel.SetSearch(args.NewTextValue);
        }
    }

    private void OnMethod(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: CatchMethodEnum method })
        {
            _viewModel.SetMethod(method);
        }
    }

    private void OnClearFilters(object? sender, EventArgs args)
    {
        _viewModel.ClearFilters();
    }

    private async void OnRecordCatch(object? sender, EventArgs args)
    {
        await _interactions.ShowModalAsync(_viewModel.Text["RecordCatch"], _viewModel.Text["RecordUnavailable"]);
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
