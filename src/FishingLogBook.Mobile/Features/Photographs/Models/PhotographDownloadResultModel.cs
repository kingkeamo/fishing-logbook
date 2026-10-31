using FishingLogBook.Mobile.Features.Photographs.Enums;

namespace FishingLogBook.Mobile.Features.Photographs.Models;

public sealed class PhotographDownloadResultModel
{
    private PhotographDownloadResultModel(PhotographDownloadOutcomeEnum outcome, byte[]? bytes)
    {
        Outcome = outcome;
        Bytes = bytes;
    }

    public PhotographDownloadOutcomeEnum Outcome { get; }

    public byte[]? Bytes { get; }

    public static PhotographDownloadResultModel Downloaded(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return new PhotographDownloadResultModel(PhotographDownloadOutcomeEnum.Downloaded, bytes);
    }

    public static PhotographDownloadResultModel Failed(PhotographDownloadOutcomeEnum outcome)
    {
        if (outcome == PhotographDownloadOutcomeEnum.Downloaded)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        return new PhotographDownloadResultModel(outcome, null);
    }
}
