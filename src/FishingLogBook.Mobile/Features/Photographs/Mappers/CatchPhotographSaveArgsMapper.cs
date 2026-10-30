using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Photographs.Models;

namespace FishingLogBook.Mobile.Features.Photographs.Mappers;

public sealed class CatchPhotographSaveArgsMapper : ICatchPhotographSaveArgsMapper
{
    public IReadOnlyList<LocalCatchPhotographSaveArgs> Map(IReadOnlyList<PhotographEditorItemModel> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items
            .OrderBy(item => item.SortOrder)
            .Select(item => new LocalCatchPhotographSaveArgs
            {
                Id = item.Id,
                SortOrder = item.SortOrder,
                LocalFilePath = item.RelativeFilePath,
                ContentType = item.ContentType
            })
            .ToArray();
    }
}
