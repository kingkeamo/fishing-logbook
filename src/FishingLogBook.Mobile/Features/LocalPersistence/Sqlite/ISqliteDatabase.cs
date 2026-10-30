using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.LocalPersistence.Sqlite;

public interface ISqliteDatabase
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken);

    Task ExecuteInTransactionAsync(
        Func<SqliteConnection, SqliteTransaction, CancellationToken, Task> action,
        CancellationToken cancellationToken);
}
