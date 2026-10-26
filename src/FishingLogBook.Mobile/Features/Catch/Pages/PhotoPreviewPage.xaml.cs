using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Catch.Pages;

public partial class PhotoPreviewPage : ContentPage, IQueryAttributable
{
    public PhotoPreviewPage()
    {
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
}
