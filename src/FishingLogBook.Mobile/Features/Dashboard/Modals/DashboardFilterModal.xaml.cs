using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Dashboard.Models;
using FishingLogBook.Mobile.Features.Dashboard.ViewModels;

namespace FishingLogBook.Mobile.Features.Dashboard.Modals;

public partial class DashboardFilterModal : Popup<DashboardFilterSelectionModel>
{
    private readonly DashboardFilterViewModel _viewModel;
    private bool _closing;
    private bool _opened;
    private bool _dismissRequested;

    public DashboardFilterModal(DashboardFilterViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
        Opened += async (_, _) =>
        {
            _opened = true;
            if (_dismissRequested)
            {
                await CompleteAsync(null);
            }
        };
        var display = DeviceDisplay.MainDisplayInfo;
        WidthRequest = Math.Min(480, display.Width / display.Density - 40);
        Sheet.HeightRequest = Math.Min(560, display.Height / display.Density * 0.82);
    }

    public async Task DismissAsync()
    {
        _dismissRequested = true;
        if (_opened)
        {
            await CompleteAsync(null);
        }
    }

    private void OnPresetChanged(object? sender, EventArgs args)
    {
        if (sender is Picker { SelectedIndex: >= 0 } picker)
        {
            _viewModel.SelectPreset(picker.SelectedIndex);
        }
    }

    private async void OnApply(object? sender, EventArgs args)
    {
        if (_viewModel.Apply() is { } selection)
        {
            await CompleteAsync(selection);
        }
    }

    private async void OnReset(object? sender, EventArgs args)
    {
        await CompleteAsync(_viewModel.Reset());
    }

    private async void OnClose(object? sender, EventArgs args)
    {
        await CompleteAsync(null);
    }

    private async Task CompleteAsync(DashboardFilterSelectionModel? selection)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        if (selection is null)
        {
            await CloseAsync();
            return;
        }

        await CloseAsync(selection);
    }
}
