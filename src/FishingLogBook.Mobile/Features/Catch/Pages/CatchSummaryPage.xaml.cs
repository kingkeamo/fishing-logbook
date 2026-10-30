using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Native.Controls;
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadUnitsAsync(CancellationToken.None);
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
        if (sender is ChipView { CommandParameter: CatchMethodEnum method })
        {
            _viewModel.SetMethod(method);
        }
    }

    private void OnOpenMenu(object? sender, EventArgs args)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    private async void OnOpenFilters(object? sender, EventArgs args)
    {
        var result = await Shell.Current.ShowPopupAsync<CatchFilterModel>(new FilterPopup(_viewModel.CreateFilterDraft()), new PopupOptions
        {
            PageOverlayColor = Color.FromArgb("#99102A43"),
            Shape = null
        });
        if (!result.WasDismissedByTappingOutsideOfPopup && result.Result is not null)
        {
            _viewModel.ApplyFilters(result.Result);
        }
    }

    private void OnClearFilters(object? sender, EventArgs args)
    {
        _viewModel.ClearFilters();
    }

    private async void OnRecordCatch(object? sender, EventArgs args)
    {
        await _navigation.NavigateAsync(MobileRouteEnum.CatchEdit);
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
