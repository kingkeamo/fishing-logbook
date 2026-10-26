using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Interactions;

namespace FishingLogBook.Mobile.Native.Controls;

public partial class InteractionPopup : Popup<bool>
{
    private bool _closing;

    public InteractionPopup(DialogRequest request)
    {
        InitializeComponent();
        BindingContext = request;
        if (request.IsDestructive)
        {
            AcceptButton.Style = (Style)Application.Current!.Resources["DestructiveButton"];
            DialogIcon.Source = "icon_trash.png";
            DialogIcon.Tone = IconToneEnum.Danger;
            IconCircle.SetAppThemeColor(VisualElement.BackgroundColorProperty, (Color)Application.Current.Resources["DangerTintLight"], (Color)Application.Current.Resources["DangerTintDark"]);
        }
    }

    private async void OnAccept(object? sender, EventArgs args)
    {
        await CompleteAsync(true);
    }

    private async void OnCancel(object? sender, EventArgs args)
    {
        await CompleteAsync(false);
    }

    private async Task CompleteAsync(bool accepted)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        await CloseAsync(accepted);
    }
}
