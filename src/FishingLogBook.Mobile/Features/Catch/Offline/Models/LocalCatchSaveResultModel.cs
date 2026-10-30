using FishingLogBook.Mobile.Features.Catch.Offline.Enums;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Models;

public sealed class LocalCatchSaveResultModel
{
    private LocalCatchSaveResultModel(
        LocalCatchSaveOutcomeEnum outcome,
        Guid? catchId,
        long? revision)
    {
        Outcome = outcome;
        CatchId = catchId;
        Revision = revision;
    }

    public LocalCatchSaveOutcomeEnum Outcome { get; }

    public Guid? CatchId { get; }

    public long? Revision { get; }

    public bool IsSuccess => Outcome == LocalCatchSaveOutcomeEnum.Success;

    public static LocalCatchSaveResultModel Saved(Guid catchId, long revision)
    {
        return new LocalCatchSaveResultModel(LocalCatchSaveOutcomeEnum.Success, catchId, revision);
    }

    public static LocalCatchSaveResultModel Failed(LocalCatchSaveOutcomeEnum outcome)
    {
        if (outcome == LocalCatchSaveOutcomeEnum.Success || !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        return new LocalCatchSaveResultModel(outcome, null, null);
    }
}
