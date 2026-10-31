namespace FishingLogBook.Mobile.Common.Offline.Constants;

public static class OutboxErrorCodeConstants
{
    public const string LeaseExpired = "LeaseExpired";
    public const string Superseded = "Superseded";
    public const string Unexpected = "Unexpected";
    public const string NoSynchroniser = "NoSynchroniser";
    public const string NotFound = "NotFound";
    public const string WaitingForCatch = "WaitingForCatch";
    public const string UploadUrlRejected = "UploadUrlRejected";
    public const string PhotographFileMissing = "PhotographFileMissing";
    public const string PhotographContentTypeMissing = "PhotographContentTypeMissing";
    public const string RetriesExhausted = "RetriesExhausted";
}
