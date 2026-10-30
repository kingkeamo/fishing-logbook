using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Common.Offline.Sqlite;

public interface ISqliteConnectionFactory
{
    Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken);
}
