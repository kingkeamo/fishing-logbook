using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Trips.ViewModels;

namespace FishingLogBook.Mobile.Features.Trips.Modals;

public partial class TripNoteModal : Popup<bool>
{
    private readonly TripNoteEditorViewModel _viewModel;
    private bool _closing;

    public TripNoteModal(TripNoteEditorViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
        WidthRequest = Math.Min(720, DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density);
    }

    private async void OnSave(object? sender, EventArgs args)
    {
        if (await _viewModel.SaveAsync(CancellationToken.None))
        {
            await CompleteAsync(true);
        }
    }

    private async void OnCancel(object? sender, EventArgs args)
    {
        await CompleteAsync(false);
    }

    private async Task CompleteAsync(bool changed)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        await CloseAsync(changed);
    }
}
