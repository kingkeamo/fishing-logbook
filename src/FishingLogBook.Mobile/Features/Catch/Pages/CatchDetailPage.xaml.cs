using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Catch.Pages;

public partial class CatchDetailPage : ContentPage, IQueryAttributable
{
    private readonly NavigationService _navigation;
    private readonly InteractionService _interactions;

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
        if (BindingContext is not CatchPresentationModel item)
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

    private async void OnPreview(object? sender, EventArgs args)
    {
        if (BindingContext is CatchPresentationModel item)
        {
            await _navigation.NavigateAsync(MobileRouteEnum.PhotoPreview, item);
        }
    }
}
