using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Catch.Pages;

public partial class PhotoPreviewPage : ContentPage, IQueryAttributable
{
    private readonly NavigationService _navigation;
    private readonly InteractionService _interactions;

    public PhotoPreviewPage(NavigationService navigation, InteractionService interactions)
    {
        _navigation = navigation;
        _interactions = interactions;
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(RouteConstants.CatchParameter, out var value) && value is CatchPresentationModel item)
        {
            BindingContext = item;
            UpdateCounter(0);
        }
    }

    private void OnPositionChanged(object? sender, PositionChangedEventArgs args)
    {
        UpdateCounter(args.CurrentPosition);
    }

    private async void OnBack(object? sender, EventArgs args)
    {
        await _navigation.GoBackAsync();
    }

    private async void OnAction(object? sender, EventArgs args)
    {
        if (BindingContext is CatchPresentationModel item)
        {
            await _interactions.NotifyAsync(NotificationKindEnum.Info, item.Text["ActionUnavailable"]);
        }
    }

    private void UpdateCounter(int position)
    {
        if (BindingContext is CatchPresentationModel { HasPhoto: true } item)
        {
            Counter.Text = string.Format(item.Text.Culture, item.Text["PhotoOf"], position + 1, item.PhotoCount);
        }
    }
}
