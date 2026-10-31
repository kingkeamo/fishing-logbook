using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.IO.Adapters;

namespace FishingLogBook.Mobile.Features.Photographs.Services;

public sealed class PhotographCollectionEditorService(
    IPhotographMediaAdapter mediaAdapter,
    IPhotographPreparationService preparationService) : IPhotographCollectionEditorService
{
    public int DefaultMaximumCount { get; } = 10;

    public Task<PhotographEditorActionResultModel> TakePhotoAsync(
        Guid userId,
        IReadOnlyList<PhotographEditorItemModel> currentItems,
        int maximumCount,
        CancellationToken cancellationToken)
    {
        return PickAndPrepareAsync(
            userId,
            currentItems,
            maximumCount,
            PhotographSourceEnum.Camera,
            mediaAdapter.CapturePhotoAsync,
            cancellationToken);
    }

    public Task<PhotographEditorActionResultModel> ChooseFromGalleryAsync(
        Guid userId,
        IReadOnlyList<PhotographEditorItemModel> currentItems,
        int maximumCount,
        CancellationToken cancellationToken)
    {
        return PickAndPrepareAsync(
            userId,
            currentItems,
            maximumCount,
            PhotographSourceEnum.Gallery,
            token => mediaAdapter.PickPhotosAsync(RemainingSlots(currentItems, maximumCount), token),
            cancellationToken);
    }

    public PhotographEditorActionResultModel Remove(
        IReadOnlyList<PhotographEditorItemModel> currentItems,
        Guid photographId)
    {
        var remaining = currentItems.Where(item => item.Id != photographId).ToArray();
        if (remaining.Length == currentItems.Count)
        {
            return PhotographEditorActionResultModel.Unchanged(currentItems, PhotographPreparationOutcomeEnum.Prepared);
        }

        return PhotographEditorActionResultModel.Updated(Renumber(remaining));
    }

    public PhotographEditorActionResultModel MoveEarlier(
        IReadOnlyList<PhotographEditorItemModel> currentItems,
        Guid photographId)
    {
        return Move(currentItems, photographId, -1);
    }

    public PhotographEditorActionResultModel MoveLater(
        IReadOnlyList<PhotographEditorItemModel> currentItems,
        Guid photographId)
    {
        return Move(currentItems, photographId, 1);
    }

    private async Task<PhotographEditorActionResultModel> PickAndPrepareAsync(
        Guid userId,
        IReadOnlyList<PhotographEditorItemModel> currentItems,
        int maximumCount,
        PhotographSourceEnum source,
        Func<CancellationToken, Task<PhotographPickResultModel>> pickAsync,
        CancellationToken cancellationToken)
    {
        if (RemainingSlots(currentItems, maximumCount) <= 0)
        {
            return PhotographEditorActionResultModel.Unchanged(currentItems, PhotographPreparationOutcomeEnum.Prepared);
        }

        var pick = await pickAsync(cancellationToken);
        return pick.Outcome switch
        {
            PhotographPickOutcomeEnum.Cancelled => PhotographEditorActionResultModel.Unchanged(
                currentItems,
                PhotographPreparationOutcomeEnum.Cancelled),
            PhotographPickOutcomeEnum.PermissionDenied => PhotographEditorActionResultModel.Unchanged(
                currentItems,
                PhotographPreparationOutcomeEnum.PermissionDenied),
            PhotographPickOutcomeEnum.Unavailable => PhotographEditorActionResultModel.Unchanged(
                currentItems,
                PhotographPreparationOutcomeEnum.CouldNotPrepare),
            _ => await PreparePickedAsync(userId, currentItems, maximumCount, source, pick.Files, cancellationToken)
        };
    }

    private async Task<PhotographEditorActionResultModel> PreparePickedAsync(
        Guid userId,
        IReadOnlyList<PhotographEditorItemModel> currentItems,
        int maximumCount,
        PhotographSourceEnum source,
        IReadOnlyList<PhotographPickedFileModel> files,
        CancellationToken cancellationToken)
    {
        var items = currentItems.ToList();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (RemainingSlots(items, maximumCount) <= 0)
            {
                break;
            }

            var prepared = await preparationService.PrepareFromFileAsync(
                userId,
                file.FullPath,
                file.ContentType,
                file.LastModified,
                source,
                cancellationToken);
            if (prepared.Outcome != PhotographPreparationOutcomeEnum.Prepared || prepared.File is null)
            {
                return PhotographEditorActionResultModel.Unchanged(items, prepared.Outcome);
            }

            items.Add(new PhotographEditorItemModel
            {
                Id = prepared.File.PhotographId,
                SortOrder = items.Count,
                PreviewPath = prepared.File.AbsolutePath,
                RelativeFilePath = prepared.File.RelativeFilePath,
                ContentType = prepared.File.ContentType,
                Metadata = prepared.Metadata
            });
        }

        return PhotographEditorActionResultModel.Updated(Renumber(items));
    }

    private static PhotographEditorActionResultModel Move(
        IReadOnlyList<PhotographEditorItemModel> currentItems,
        Guid photographId,
        int direction)
    {
        var items = currentItems.OrderBy(item => item.SortOrder).ToList();
        var index = items.FindIndex(item => item.Id == photographId);
        if (index < 0)
        {
            return PhotographEditorActionResultModel.Unchanged(currentItems, PhotographPreparationOutcomeEnum.Prepared);
        }

        var target = index + direction;
        if (target < 0 || target >= items.Count)
        {
            return PhotographEditorActionResultModel.Unchanged(currentItems, PhotographPreparationOutcomeEnum.Prepared);
        }

        (items[index], items[target]) = (items[target], items[index]);
        return PhotographEditorActionResultModel.Updated(Renumber(items));
    }

    private static int RemainingSlots(IReadOnlyList<PhotographEditorItemModel> items, int maximumCount)
    {
        return Math.Max(0, maximumCount - items.Count);
    }

    private static IReadOnlyList<PhotographEditorItemModel> Renumber(IReadOnlyList<PhotographEditorItemModel> items)
    {
        return items
            .Select((item, index) => new PhotographEditorItemModel
            {
                Id = item.Id,
                SortOrder = index,
                PreviewPath = item.PreviewPath,
                RelativeFilePath = item.RelativeFilePath,
                ContentType = item.ContentType
            })
            .ToArray();
    }
}
