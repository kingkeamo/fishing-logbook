using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Features.Catch.Pages;

public partial class PhotoPreviewPage : ContentPage, IQueryAttributable
{
    public PhotoPreviewPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("catch", out var value) && value is CatchPresentationModel item)
        {
            BindingContext = item;
        }
    }
}
