using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Import.ViewModels;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Import.Modals;

public partial class ImportAnglerModal : Popup<AnglerSummaryDto>
{
    private bool _closing;

    public ImportAnglerModal(ImportAnglerModalViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        WidthRequest = Math.Min(480, (DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density) - 40);
    }

    private async void OnSearch(object? sender, EventArgs args)
    {
        if (BindingContext is ImportAnglerModalViewModel viewModel)
        {
            await viewModel.SearchAsync(CancellationToken.None);
        }
    }

    private async void OnSelect(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid userId }
            && BindingContext is ImportAnglerModalViewModel viewModel)
        {
            var angler = viewModel.Results.FirstOrDefault(candidate => candidate.UserId == userId);
            if (angler is not null)
            {
                await CloseOnceAsync(angler);
            }
        }
    }

    private async void OnDismiss(object? sender, EventArgs args)
    {
        await CloseOnceAsync(null);
    }

    private async Task CloseOnceAsync(AnglerSummaryDto? angler)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        if (angler is null)
        {
            await CloseAsync();
            return;
        }

        await CloseAsync(angler);
    }
}
