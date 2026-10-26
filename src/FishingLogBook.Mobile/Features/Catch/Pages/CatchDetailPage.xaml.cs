using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Catch.Pages;

public partial class CatchDetailPage : ContentPage, IQueryAttributable
{
    private readonly NavigationService _navigation;

    public CatchDetailPage(NavigationService navigation)
    {
        _navigation = navigation;
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(RouteConstants.CatchParameter, out var value) && value is CatchPresentationModel item)
        {
            BindingContext = item;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var item = BindingContext;
        BindingContext = null;
        BindingContext = item;
    }

    private async void OnPreview(object? sender, EventArgs args)
    {
        if (BindingContext is CatchPresentationModel item)
        {
            await _navigation.NavigateAsync(MobileRouteEnum.PhotoPreview, item);
        }
    }
}
