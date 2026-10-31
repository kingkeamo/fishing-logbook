using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using FishingLogBook.Mobile.Features.Catch.Enums;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.Popups;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Navigation;
using Microsoft.Maui.Controls.Shapes;

namespace FishingLogBook.Mobile.Features.Catch.Pages;

public partial class CatchDetailPage : ContentPage, IQueryAttributable
{
    private readonly NavigationService _navigation;
    private readonly InteractionService _interactions;
    private bool _showingActions;

    public CatchDetailPage(NavigationService navigation, InteractionService interactions)
    {
        _navigation = navigation;
        _interactions = interactions;
        InitializeComponent();
    }

    private async void OnBack(object? sender, EventArgs args)
    {
        await _navigation.GoBackAsync();
    }

    private async void OnMore(object? sender, EventArgs args)
    {
        if (_showingActions || BindingContext is not CatchPresentationModel item)
        {
            return;
        }

        _showingActions = true;
        CatchDetailActionEnum action;
        try
        {
            var result = await this.ShowPopupAsync<CatchDetailActionEnum>(new CatchActionsPopup(item),
                new PopupOptions
                {
                    PageOverlayColor = Color.FromArgb("#99102A43"),
                    Shape = new RoundRectangle(),
                    Shadow = null
                });
            action = result.Result;
        }
        finally
        {
            _showingActions = false;
        }

        await HandleActionAsync(action, item);
    }

    private async Task HandleActionAsync(CatchDetailActionEnum action, CatchPresentationModel item)
    {
        if (action == CatchDetailActionEnum.Edit)
        {
            await OpenEditorAsync();
            return;
        }

        if (action == CatchDetailActionEnum.Share)
        {
            await _interactions.NotifyAsync(NotificationKindEnum.Info, item.Text["ActionUnavailable"]);
            return;
        }

        if (action != CatchDetailActionEnum.Remove)
        {
            return;
        }

        var confirmed = await _interactions.ConfirmAsync(new DialogRequest(item.Text["RemoveCatchTitle"], item.Text["RemoveCatchBody"], item.Text["Remove"], item.Text["Cancel"], true));
        if (confirmed)
        {
            await _interactions.NotifyAsync(NotificationKindEnum.Info, item.Text["ActionUnavailable"]);
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(RouteConstants.CatchParameter, out var value) && value is CatchPresentationModel item)
        {
            BindingContext = item;
        }
    }

    private async void OnEditCatch(object? sender, EventArgs args)
    {
        await OpenEditorAsync();
    }

    private async Task OpenEditorAsync()
    {
        if (BindingContext is CatchPresentationModel item)
        {
            await _navigation.NavigateAsync(MobileRouteEnum.CatchEdit, catchEditId: item.Id);
        }
    }
}
