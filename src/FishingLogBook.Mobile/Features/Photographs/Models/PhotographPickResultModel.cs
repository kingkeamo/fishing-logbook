using FishingLogBook.Mobile.Features.Photographs.Enums;

namespace FishingLogBook.Mobile.Features.Photographs.Models;

public sealed class PhotographPickResultModel
{
    public required PhotographPickOutcomeEnum Outcome { get; init; }

    public IReadOnlyList<PhotographPickedFileModel> Files { get; init; } = Array.Empty<PhotographPickedFileModel>();

    public static PhotographPickResultModel Cancelled { get; } = new() { Outcome = PhotographPickOutcomeEnum.Cancelled };

    public static PhotographPickResultModel PermissionDenied { get; } = new()
    {
        Outcome = PhotographPickOutcomeEnum.PermissionDenied
    };

    public static PhotographPickResultModel Unavailable { get; } = new()
    {
        Outcome = PhotographPickOutcomeEnum.Unavailable
    };

    public static PhotographPickResultModel FromFiles(IReadOnlyList<PhotographPickedFileModel> files)
    {
        return new PhotographPickResultModel
        {
            Outcome = PhotographPickOutcomeEnum.Selected,
            Files = files
        };
    }
}

public sealed class PhotographPickedFileModel
{
    public required string FullPath { get; init; }

    public required string ContentType { get; init; }

    public DateTimeOffset? LastModified { get; init; }
}
