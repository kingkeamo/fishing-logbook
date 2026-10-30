using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Photographs.Models;

namespace FishingLogBook.Mobile.Features.Photographs.Mappers;

public interface ICatchPhotographSaveArgsMapper
{
    IReadOnlyList<LocalCatchPhotographSaveArgs> Map(IReadOnlyList<PhotographEditorItemModel> items);
}
