namespace FishingLogBook.Mobile.Common.Offline.Enums;

public enum OutboxOperationOutcomeEnum
{
    Completed = 0,
    Retry = 1,
    Terminal = 2,
    Paused = 3
}
