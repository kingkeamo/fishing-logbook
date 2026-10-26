using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Navigation;
using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Features.Showcase.Pages;

public partial class ShowcasePage : ContentPage
{
    private readonly UiTextService _text;
    private readonly InteractionService _interactions;
    private readonly CatchListViewModel _catches;
    private readonly NavigationService _navigation;

    public ShowcasePage(UiTextService text, InteractionService interactions, CatchListViewModel catches, NavigationService navigation)
    {
        _text = text;
        _interactions = interactions;
        _catches = catches;
        _navigation = navigation;
        InitializeComponent();
        BindingContext = text;
    }

    private async void OnOpenControls(object? sender, EventArgs args)
    {
        await _navigation.NavigateAsync(MobileRouteEnum.MeasurementShowcase);
    }

    private async void OnNotify(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: string kind })
        {
            await _interactions.NotifyAsync(Enum.Parse<NotificationKindEnum>(kind), _text["NotificationDemo"]);
        }
    }

    private async void OnConfirm(object? sender, EventArgs args)
    {
        var accepted = await _interactions.ConfirmAsync(new DialogRequest(_text["ConfirmTitle"], _text["ConfirmBody"], _text["ConfirmAction"], _text["Cancel"], true));
        await _interactions.NotifyAsync(NotificationKindEnum.Info, _text[accepted ? "Accepted" : "Cancelled"]);
    }

    private async void OnModal(object? sender, EventArgs args)
    {
        var accepted = await _interactions.ShowModalAsync(_text["ModalTitle"], _text["ModalBody"]);
        await _interactions.NotifyAsync(NotificationKindEnum.Info, _text[accepted ? "Accepted" : "Cancelled"]);
    }

    private async void OnState(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: string state })
        {
            _catches.SetState(Enum.Parse<ContentStateEnum>(state));
            await _interactions.NotifyAsync(NotificationKindEnum.Info, _text["StateDemoNotice"]);
        }
    }

    private async void OnCancelModal(object? sender, EventArgs args)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await _interactions.ShowModalAsync(_text["ModalTitle"], _text["CancelModalBody"], cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            await _interactions.NotifyAsync(NotificationKindEnum.Info, _text["Cancelled"]);
        }
    }
}
