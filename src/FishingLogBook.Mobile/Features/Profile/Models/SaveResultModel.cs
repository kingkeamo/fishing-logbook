using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;

namespace FishingLogBook.Mobile.Features.Profile.Models;

public sealed record SaveResultModel<T>
    where T : class
{
    private readonly ApiResultModel<T> _result;

    private SaveResultModel(ApiResultModel<T> result, bool mayHavePartiallySaved)
    {
        _result = result;
        MayHavePartiallySaved = mayHavePartiallySaved;
    }

    public bool MayHavePartiallySaved { get; }
    public bool IsSuccess => _result.IsSuccess;
    public ApiOutcomeEnum Outcome => _result.Outcome;
    public T? Value => _result.Value;

    public static SaveResultModel<T> Saved(T value)
    {
        return new SaveResultModel<T>(ApiResultModel<T>.Succeeded(value), false);
    }

    public static SaveResultModel<T> NotAttempted(ApiOutcomeEnum outcome)
    {
        return new SaveResultModel<T>(ApiResultModel<T>.Failed(outcome), false);
    }

    public static SaveResultModel<T> WriteFailed(ApiOutcomeEnum outcome, bool earlierWritesSaved)
    {
        // A timed-out or dropped write may still have been applied by the server.
        return new SaveResultModel<T>(
            ApiResultModel<T>.Failed(outcome),
            earlierWritesSaved || outcome == ApiOutcomeEnum.Unavailable);
    }
}
