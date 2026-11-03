using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Import.Services;

public interface IImportModalService
{
    Task<PlaceNameModalResult?> EditPlaceNameAsync(PlaceNameModalModel model);

    Task<Guid?> ChooseExistingTripAsync(IReadOnlyList<TripSummaryDto> trips);

    Task<AnglerSummaryDto?> ChooseAnglerAsync(IReadOnlyList<Guid> excludedUserIds);
}
