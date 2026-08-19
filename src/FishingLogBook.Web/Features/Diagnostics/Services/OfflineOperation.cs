using System.Diagnostics;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Storage;

namespace FishingLogBook.Web.Features.Diagnostics.Services;

public static class OfflineOperation
{
    public static async Task ExecuteAsync(
        string operation,
        string storeName,
        string startedEvent,
        string completedEvent,
        string failedEvent,
        string timedOutEvent,
        TimeSpan timeout,
        IDiagnosticLogger diagnostics,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken,
        ILoggingService? logging = null)
    {
        await ExecuteAsync(
            operation,
            storeName,
            startedEvent,
            completedEvent,
            failedEvent,
            timedOutEvent,
            timeout,
            diagnostics,
            async token =>
            {
                await action(token);
                return 0;
            },
            cancellationToken,
            logging);
    }

    public static async Task<T> ExecuteAsync<T>(
        string operation,
        string storeName,
        string startedEvent,
        string completedEvent,
        string failedEvent,
        string timedOutEvent,
        TimeSpan timeout,
        IDiagnosticLogger diagnostics,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        ILoggingService? logging = null)
    {
        var metadata = StartingMetadata(operation, storeName, timeout);
        TryToLog(diagnostics, logging, DiagnosticLevel.Debug, startedEvent, $"{operation} started.", metadata, null, cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            var result = await action(timeoutSource.Token);
            stopwatch.Stop();
            TryToLog(
                diagnostics,
                logging,
                DiagnosticLevel.Debug,
                completedEvent,
                $"{operation} completed.",
                CompletedMetadata(metadata, stopwatch.ElapsedMilliseconds, result),
                null,
                cancellationToken);
            return result;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            TryToLog(
                diagnostics,
                logging,
                DiagnosticLevel.Warning,
                timedOutEvent,
                $"{operation} timed out.",
                FailedMetadata(metadata, stopwatch.ElapsedMilliseconds, exception),
                exception,
                cancellationToken);
            throw new TimeoutException($"{operation} timed out after {timeout.TotalMilliseconds:0}ms.", exception);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            var timedOut = IsTimeout(exception);
            TryToLog(
                diagnostics,
                logging,
                timedOut ? DiagnosticLevel.Warning : DiagnosticLevel.Error,
                timedOut ? timedOutEvent : failedEvent,
                timedOut ? $"{operation} timed out." : $"{operation} failed.",
                FailedMetadata(metadata, stopwatch.ElapsedMilliseconds, exception),
                exception,
                cancellationToken);
            throw;
        }
    }

    private static Dictionary<string, string> StartingMetadata(string operation, string storeName, TimeSpan timeout)
    {
        return new Dictionary<string, string>
        {
            [DiagnosticMetadata.Operation] = operation,
            [DiagnosticMetadata.StoreName] = storeName,
            [DiagnosticMetadata.TimeoutMilliseconds] = ((int)timeout.TotalMilliseconds).ToString()
        };
    }

    private static Dictionary<string, string> CompletedMetadata(Dictionary<string, string> started, long elapsedMilliseconds, object? result)
    {
        var metadata = new Dictionary<string, string>(started)
        {
            [DiagnosticMetadata.ElapsedMilliseconds] = elapsedMilliseconds.ToString(),
            [DiagnosticMetadata.Result] = "completed"
        };

        if (result is int count)
        {
            metadata[DiagnosticMetadata.RecordCount] = count.ToString();
        }
        else if (result is IReadOnlyCollection<string> items)
        {
            metadata[DiagnosticMetadata.RecordCount] = items.Count.ToString();
        }

        return metadata;
    }

    private static Dictionary<string, string> FailedMetadata(Dictionary<string, string> started, long elapsedMilliseconds, Exception exception)
    {
        return new Dictionary<string, string>(started)
        {
            [DiagnosticMetadata.ElapsedMilliseconds] = elapsedMilliseconds.ToString(),
            [DiagnosticMetadata.ErrorType] = exception.GetType().Name,
            [DiagnosticMetadata.Result] = "failed"
        };
    }

    private static bool IsTimeout(Exception exception)
    {
        return exception is TimeoutException ||
               exception.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryToLog(
        IDiagnosticLogger diagnostics,
        ILoggingService? logging,
        DiagnosticLevel level,
        string eventName,
        string message,
        IReadOnlyDictionary<string, string> metadata,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        _ = SafeLogAsync(diagnostics, logging, level, eventName, message, metadata, exception, cancellationToken);
    }

    private static async Task SafeLogAsync(
        IDiagnosticLogger diagnostics,
        ILoggingService? logging,
        DiagnosticLevel level,
        string eventName,
        string message,
        IReadOnlyDictionary<string, string> metadata,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        try
        {
            await diagnostics.LogAsync(level, eventName, message, metadata, exception, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception loggingException)
        {
            if (logging is not null)
            {
                await logging.LogErrorAsync("diagnostic log", loggingException, CancellationToken.None);
            }
        }
    }
}
