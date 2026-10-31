using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;

namespace FishingLogBook.Mobile.Common.Offline.Mappers;

public static class OutboxApiOutcomeMapper
{
    public static OutboxOperationResultModel ToFailure(ApiOutcomeEnum outcome)
    {
        return outcome switch
        {
            ApiOutcomeEnum.Success => throw new ArgumentOutOfRangeException(nameof(outcome)),
            ApiOutcomeEnum.SessionExpired => OutboxOperationResultModel.Paused(),
            ApiOutcomeEnum.ValidationFailed or ApiOutcomeEnum.Forbidden =>
                OutboxOperationResultModel.Terminal(outcome.ToString()),
            _ => OutboxOperationResultModel.Retry(outcome.ToString())
        };
    }
}
