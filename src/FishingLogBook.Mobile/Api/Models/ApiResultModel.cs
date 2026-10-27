using FishingLogBook.Mobile.Api.Enums;

namespace FishingLogBook.Mobile.Api.Models;

public sealed record ApiResultModel<T>
    where T : class
{
    private ApiResultModel(ApiOutcomeEnum outcome, T? value)
    {
        Outcome = outcome;
        Value = value;
    }

    public ApiOutcomeEnum Outcome { get; }
    public T? Value { get; }
    public bool IsSuccess => Outcome == ApiOutcomeEnum.Success;

    public static ApiResultModel<T> Succeeded(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ApiResultModel<T>(ApiOutcomeEnum.Success, value);
    }

    public static ApiResultModel<T> Failed(ApiOutcomeEnum outcome)
    {
        if (outcome == ApiOutcomeEnum.Success || !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        return new ApiResultModel<T>(outcome, null);
    }
}
