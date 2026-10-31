using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Catch.Enums;
using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Features.Catch.Popups;

public partial class CatchActionsPopup : Popup<CatchDetailActionEnum>
{
    private bool _closing;

    public CatchActionsPopup(CatchPresentationModel item)
    {
        InitializeComponent();
        BindingContext = item;
        WidthRequest = Math.Min(720, DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density);
    }

    private async void OnAction(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: CatchDetailActionEnum action })
        {
            await CompleteAsync(action);
        }
    }

    private async void OnCancel(object? sender, EventArgs args)
    {
        await CompleteAsync(CatchDetailActionEnum.None);
    }

    private async Task CompleteAsync(CatchDetailActionEnum action)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        await CloseAsync(action);
    }
}
