using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Catch.Services;

public interface ICatchHistoryService
{
    Task<CatchHistoryModel> LoadAsync(UiTextService text, CancellationToken cancellationToken);
}
