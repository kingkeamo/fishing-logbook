using FishingLogBook.Mobile.Common.Offline.Enums;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Common.Offline.Models;

public sealed class OutboxOperationResultModel
{
    private OutboxOperationResultModel(
        OutboxOperationOutcomeEnum outcome,
        string? errorCode,
        Func<SqliteConnection, SqliteTransaction, CancellationToken, Task>? persist)
    {
        Outcome = outcome;
        ErrorCode = errorCode;
        Persist = persist;
    }

    public OutboxOperationOutcomeEnum Outcome { get; }

    public string? ErrorCode { get; }

    public Func<SqliteConnection, SqliteTransaction, CancellationToken, Task>? Persist { get; }

    public static OutboxOperationResultModel Completed(
        Func<SqliteConnection, SqliteTransaction, CancellationToken, Task>? persist = null)
    {
        return new OutboxOperationResultModel(OutboxOperationOutcomeEnum.Completed, null, persist);
    }

    public static OutboxOperationResultModel Retry(string errorCode)
    {
        return new OutboxOperationResultModel(OutboxOperationOutcomeEnum.Retry, errorCode, null);
    }

    public static OutboxOperationResultModel Terminal(string errorCode)
    {
        return new OutboxOperationResultModel(OutboxOperationOutcomeEnum.Terminal, errorCode, null);
    }

    public static OutboxOperationResultModel Paused()
    {
        return new OutboxOperationResultModel(OutboxOperationOutcomeEnum.Paused, null, null);
    }
}
