using FishingLogBook.Web.Features.Photographs.Enums;

namespace FishingLogBook.Web.Features.Photographs.Models;

public sealed record PhotographPreparationModel(
    PreparedPhotographModel? Photograph,
    PhotographPreparationOutcomeEnum Outcome)
{
    public static PhotographPreparationModel Prepared(PreparedPhotographModel photograph)
    {
        return new PhotographPreparationModel(photograph, PhotographPreparationOutcomeEnum.Prepared);
    }

    public static PhotographPreparationModel Unsupported { get; } =
        new(null, PhotographPreparationOutcomeEnum.UnsupportedContentType);

    public static PhotographPreparationModel CouldNotPrepare { get; } =
        new(null, PhotographPreparationOutcomeEnum.CouldNotPrepare);
}
