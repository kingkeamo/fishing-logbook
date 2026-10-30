using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;

namespace FishingLogBook.Mobile.Features.Photographs.Services;

public interface IPhotographCollectionEditorService
{
    int DefaultMaximumCount { get; }

    Task<PhotographEditorActionResultModel> TakePhotoAsync(
        Guid userId,
        IReadOnlyList<PhotographEditorItemModel> currentItems,
        int maximumCount,
        CancellationToken cancellationToken);

    Task<PhotographEditorActionResultModel> ChooseFromGalleryAsync(
        Guid userId,
        IReadOnlyList<PhotographEditorItemModel> currentItems,
        int maximumCount,
        CancellationToken cancellationToken);

    PhotographEditorActionResultModel Remove(
        IReadOnlyList<PhotographEditorItemModel> currentItems,
        Guid photographId);

    PhotographEditorActionResultModel MoveEarlier(
        IReadOnlyList<PhotographEditorItemModel> currentItems,
        Guid photographId);

    PhotographEditorActionResultModel MoveLater(
        IReadOnlyList<PhotographEditorItemModel> currentItems,
        Guid photographId);
}

public sealed class PhotographEditorActionResultModel
{
    public required PhotographPreparationOutcomeEnum Outcome { get; init; }

    public IReadOnlyList<PhotographEditorItemModel> Items { get; init; } = Array.Empty<PhotographEditorItemModel>();

    public static PhotographEditorActionResultModel Unchanged(
        IReadOnlyList<PhotographEditorItemModel> items,
        PhotographPreparationOutcomeEnum outcome)
    {
        return new PhotographEditorActionResultModel { Outcome = outcome, Items = items };
    }

    public static PhotographEditorActionResultModel Updated(IReadOnlyList<PhotographEditorItemModel> items)
    {
        return new PhotographEditorActionResultModel
        {
            Outcome = PhotographPreparationOutcomeEnum.Prepared,
            Items = items
        };
    }
}
