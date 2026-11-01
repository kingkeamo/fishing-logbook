using CommunityToolkit.Maui.Views;

namespace FishingLogBook.Mobile.Features.Profile.Popups;

public partial class DisplayNamePopup : Popup<bool>
{
    public DisplayNamePopup(string displayName)
    {
        InitializeComponent();
        NameLabel.Text = displayName;
    }
}
