using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Common.Offline.Sqlite;

public sealed class SqliteDatabase : ISqliteDatabase, IDisposable
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly ISqliteMigrator _migrator;
    private readonly ILogger<SqliteDatabase> _logger;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _initLock = new();
    private Task? _initializeTask;
    private bool _disposed;

    public SqliteDatabase(
        ISqliteConnectionFactory connectionFactory,
        ISqliteMigrator migrator,
        ILogger<SqliteDatabase> logger)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(migrator);
        ArgumentNullException.ThrowIfNull(logger);
        _connectionFactory = connectionFactory;
        _migrator = migrator;
        _logger = logger;
    }

    public Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Task initializeTask;
        lock (_initLock)
        {
            _initializeTask ??= InitializeCoreAsync();
            initializeTask = _initializeTask;
        }

        return WaitForInitializationAsync(initializeTask, cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(
        Func<SqliteConnection, SqliteTransaction, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsureInitializedAsync(cancellationToken);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await action(connection, transaction, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (Exception rollbackException)
                {
                    _logger.LogError(
                        rollbackException,
                        "SQLite transaction rollback failed after an error.");
                }

                if (exception is not OperationCanceledException)
                {
                    _logger.LogError(exception, "SQLite transactional write failed.");
                }

                throw;
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writeGate.Dispose();
    }

    private async Task InitializeCoreAsync()
    {
        await _writeGate.WaitAsync();
        try
        {
            await _migrator.MigrateAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Local SQLite database initialisation failed.");
            lock (_initLock)
            {
                _initializeTask = null;
            }

            throw;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private static async Task WaitForInitializationAsync(Task initializeTask, CancellationToken cancellationToken)
    {
        if (initializeTask.IsCompleted)
        {
            await initializeTask;
            return;
        }

        var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
        var completed = await Task.WhenAny(initializeTask, cancellationTask);
        if (completed != initializeTask)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        await initializeTask;
    }
}
