namespace FishingLogBook.Mobile.Api.Enums;

public enum ApiOutcomeEnum
{
    Success = 0,
    ValidationFailed = 1,
    SessionExpired = 2,
    Forbidden = 3,
    Unavailable = 4,
    ServerError = 5,
    InvalidResponse = 6
}
