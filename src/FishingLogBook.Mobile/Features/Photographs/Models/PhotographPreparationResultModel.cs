using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Shared.Photographs.Models;

namespace FishingLogBook.Mobile.Features.Photographs.Models;

public sealed class PhotographPreparationResultModel
{
    public required PhotographPreparationOutcomeEnum Outcome { get; init; }

    public PhotographPreparedFileModel? File { get; init; }

    public PhotographMetadataModel Metadata { get; init; } = PhotographMetadataModel.Empty;

    public static PhotographPreparationResultModel Unsupported { get; } = new()
    {
        Outcome = PhotographPreparationOutcomeEnum.UnsupportedContentType
    };

    public static PhotographPreparationResultModel CouldNotPrepare { get; } = new()
    {
        Outcome = PhotographPreparationOutcomeEnum.CouldNotPrepare
    };

    public static PhotographPreparationResultModel TooLarge { get; } = new()
    {
        Outcome = PhotographPreparationOutcomeEnum.TooLarge
    };

    public static PhotographPreparationResultModel Prepared(
        PhotographPreparedFileModel file,
        PhotographMetadataModel metadata)
    {
        return new PhotographPreparationResultModel
        {
            Outcome = PhotographPreparationOutcomeEnum.Prepared,
            File = file,
            Metadata = metadata
        };
    }
}

public sealed class PhotographPreparedFileModel
{
    public required Guid PhotographId { get; init; }

    public required string RelativeFilePath { get; init; }

    public required string ContentType { get; init; }

    public required string AbsolutePath { get; init; }
}
