namespace FishingLogBook.Shared.Diagnostics;

public static class DiagnosticEventNames
{
    public const string OfflineDbOpenStarted = "OfflineDbOpenStarted";
    public const string OfflineDbOpenCompleted = "OfflineDbOpenCompleted";
    public const string OfflineDbOpenFailed = "OfflineDbOpenFailed";
    public const string OfflineDbOpenTimedOut = "OfflineDbOpenTimedOut";

    public const string OfflineDbReadStarted = "OfflineDbReadStarted";
    public const string OfflineDbReadCompleted = "OfflineDbReadCompleted";
    public const string OfflineDbReadFailed = "OfflineDbReadFailed";
    public const string OfflineDbReadTimedOut = "OfflineDbReadTimedOut";

    public const string OfflineDbWriteStarted = "OfflineDbWriteStarted";
    public const string OfflineDbWriteCompleted = "OfflineDbWriteCompleted";
    public const string OfflineDbWriteFailed = "OfflineDbWriteFailed";
    public const string OfflineDbWriteTimedOut = "OfflineDbWriteTimedOut";

    public const string OfflineDbTransactionStarted = "OfflineDbTransactionStarted";
    public const string OfflineDbTransactionCompleted = "OfflineDbTransactionCompleted";
    public const string OfflineDbTransactionAborted = "OfflineDbTransactionAborted";
    public const string OfflineDbTransactionError = "OfflineDbTransactionError";
    public const string OfflineDbRequestSucceeded = "OfflineDbRequestSucceeded";
    public const string OfflineDbClosed = "OfflineDbClosed";

    public const string CatchOfflineSaveStarted = "CatchOfflineSaveStarted";
    public const string CatchOfflineSaveCompleted = "CatchOfflineSaveCompleted";
    public const string CatchOfflineSaveFailed = "CatchOfflineSaveFailed";

    public const string CatchOfflineLoadStarted = "CatchOfflineLoadStarted";
    public const string CatchOfflineLoadCompleted = "CatchOfflineLoadCompleted";

    public const string PhotoOfflineSaveStarted = "PhotoOfflineSaveStarted";
    public const string PhotoOfflineSaveCompleted = "PhotoOfflineSaveCompleted";
    public const string PhotoOfflineSaveFailed = "PhotoOfflineSaveFailed";

    public const string SyncStarted = "SyncStarted";
    public const string SyncCompleted = "SyncCompleted";
    public const string SyncFailed = "SyncFailed";
    public const string SyncRetry = "SyncRetry";

    public const string ServiceWorkerError = "ServiceWorkerError";

    public const string LocationPermissionDenied = "LocationPermissionDenied";
    public const string LocationCaptureFailed = "LocationCaptureFailed";
}
