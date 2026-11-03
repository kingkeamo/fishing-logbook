using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.Services;
using FishingLogBook.Mobile.Features.Import.ViewModels;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Import.Modals;

public sealed class ImportModalService(
    UiTextService text,
    IAnglerLookupClient lookup,
    IConnectivityService connectivity) : IImportModalService
{
    public async Task<PlaceNameModalResult?> EditPlaceNameAsync(PlaceNameModalModel model)
    {
        var viewModel = new PlaceNameModalViewModel(text, model);
        var result = await Shell.Current.ShowPopupAsync<PlaceNameModalResult>(new PlaceNameModal(viewModel));
        return result.WasDismissedByTappingOutsideOfPopup ? null : result.Result;
    }

    public async Task<Guid?> ChooseExistingTripAsync(IReadOnlyList<TripSummaryDto> trips)
    {
        var viewModel = new ExistingTripModalViewModel(text, trips);
        var result = await Shell.Current.ShowPopupAsync<ExistingTripItemModel>(new ExistingTripModal(viewModel));
        return result.WasDismissedByTappingOutsideOfPopup || result.Result is null
            ? null
            : result.Result.TripId;
    }

    public async Task<AnglerSummaryDto?> ChooseAnglerAsync(IReadOnlyList<Guid> excludedUserIds)
    {
        var viewModel = new ImportAnglerModalViewModel(lookup, connectivity, text, excludedUserIds);
        var result = await Shell.Current.ShowPopupAsync<AnglerSummaryDto>(new ImportAnglerModal(viewModel));
        return result.WasDismissedByTappingOutsideOfPopup ? null : result.Result;
    }
}
