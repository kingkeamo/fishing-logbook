using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Features.Catch.Pages;

public partial class CatchDetailPage : ContentPage, IQueryAttributable
{
    private bool _navigating;

    public CatchDetailPage()
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

    private async void OnPreview(object? sender, EventArgs args)
    {
        if (_navigating || BindingContext is not CatchPresentationModel { HasPhoto: true } item)
        {
            return;
        }

        _navigating = true;
        try
        {
            await Shell.Current.GoToAsync(nameof(PhotoPreviewPage), new ShellNavigationQueryParameters { ["catch"] = item });
        }
        finally
        {
            _navigating = false;
        }
    }
}
