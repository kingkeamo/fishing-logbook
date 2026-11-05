using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using FishingLogBook.Mobile.Features.Dashboard.Models;
using FishingLogBook.Mobile.Features.Dashboard.Services;

namespace FishingLogBook.Mobile.Features.Dashboard.Modals;

public sealed class DashboardModalService(IDashboardModalViewModelFactory viewModels) : IDashboardModalService
{
    public async Task<DashboardFilterSelectionModel?> EditFiltersAsync(
        DashboardFilterSelectionModel selection, DashboardFilterOptionsModel options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var editor = viewModels.CreateFilters();
        editor.Initialize(selection, options);
        var modal = new DashboardFilterModal(editor);
        using var registration = cancellationToken.Register(() =>
            MainThread.BeginInvokeOnMainThread(async () => await modal.DismissAsync()));
        var result = await Shell.Current.ShowPopupAsync<DashboardFilterSelectionModel>(modal, new PopupOptions
        {
            PageOverlayColor = Color.FromArgb("#99102A43"),
            Shape = null
        });
        return cancellationToken.IsCancellationRequested || result.WasDismissedByTappingOutsideOfPopup ? null : result.Result;
    }
}
