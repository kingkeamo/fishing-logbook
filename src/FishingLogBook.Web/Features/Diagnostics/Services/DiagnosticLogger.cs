using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.SystemStatus.Services;
using FishingLogBook.Web.Features.TestCatch.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.Diagnostics.Services;

public sealed class DiagnosticLogger : IDiagnosticLogger
{
    private static readonly AsyncLocal<bool> IsWriting = new();

    private readonly IDiagnosticEventStore _store;
    private readonly DiagnosticStatusModel _status;
    private readonly DiagnosticsClientConfig _config;
    private readonly CorrelationContext _correlationContext;
    private readonly INetworkService _networkStatus;
    private readonly NavigationManager _navigationManager;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILoggingService _logging;
    private readonly DiagnosticLevel _minimumPersistLevel;
    private readonly string _appVersion;
    private Guid? _anonymousSessionId;

    public DiagnosticLogger(
        IDiagnosticEventStore store,
        DiagnosticStatusModel status,
        DiagnosticsClientConfig config,
        CorrelationContext correlationContext,
        INetworkService networkStatus,
        NavigationManager navigationManager,
        IJSRuntime jsRuntime,
        ILoggingService logging)
    {
        _store = store;
        _status = status;
        _config = config;
        _correlationContext = correlationContext;
        _networkStatus = networkStatus;
        _navigationManager = navigationManager;
        _jsRuntime = jsRuntime;
        _logging = logging;
        _minimumPersistLevel = Enum.TryParse(config.MinimumPersistLevel, true, out DiagnosticLevel level)
            ? level
            : DiagnosticLevel.Warning;
        _appVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown";
    }

    public async Task LogAsync(
        DiagnosticLevel level,
        string eventName,
        string message,
        IReadOnlyDictionary<string, string>? metadata = null,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await WriteConsoleAsync(level, eventName, message, cancellationToken);

            if (level < _minimumPersistLevel)
            {
                return;
            }

            if (IsWriting.Value)
            {
                return;
            }

            IsWriting.Value = true;
            try
            {
                var persistTask = PersistQueuedEventAsync(
                    level,
                    eventName,
                    message,
                    metadata,
                    exception,
                    cancellationToken);
                var completed = await Task.WhenAny(persistTask, Task.Delay(_config.OperationTimeout, CancellationToken.None));
                if (completed != persistTask)
                {
                    await RecordFailureAsync(new TimeoutException("Diagnostic persistence timed out."), eventName, cancellationToken);
                    await WriteConsoleAsync(
                        DiagnosticLevel.Error,
                        eventName,
                        "Diagnostic persistence timed out.",
                        cancellationToken);
                    return;
                }

                await persistTask;
            }
            finally
            {
                IsWriting.Value = false;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exceptionWhileLogging)
        {
            await RecordFailureAsync(exceptionWhileLogging, eventName, CancellationToken.None);
            await WriteConsoleAsync(DiagnosticLevel.Error, eventName, "Diagnostic persistence failed.", CancellationToken.None);
        }
    }

    private async Task PersistQueuedEventAsync(
        DiagnosticLevel level,
        string eventName,
        string message,
        IReadOnlyDictionary<string, string>? metadata,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        var diagnosticEvent = await CreateEventAsync(
            level,
            eventName,
            message,
            metadata,
            exception,
            cancellationToken);
        await _store.EnqueueAsync(diagnosticEvent, cancellationToken);
        _status.QueuedCount = await _store.GetCountAsync(cancellationToken);
    }

    private async Task RecordFailureAsync(Exception exception, string eventName, CancellationToken cancellationToken)
    {
        _status.RecordFailure(DiagnosticOperations.Persist, exception);
        await _logging.LogErrorAsync(eventName, exception, cancellationToken);
    }

    private async Task<DiagnosticEventModel> CreateEventAsync(
        DiagnosticLevel level,
        string eventName,
        string message,
        IReadOnlyDictionary<string, string>? metadata,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        var isOnline = false;
        try
        {
            isOnline = await _networkStatus.IsOnlineAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception onlineCheckException)
        {
            await _logging.LogErrorAsync("diagnostic online check", onlineCheckException, CancellationToken.None);
        }

        return new DiagnosticEventModel
        {
            Id = Guid.NewGuid(),
            TimestampUtc = DateTimeOffset.UtcNow,
            Level = level,
            EventName = eventName,
            Message = DiagnosticMetadata.Truncate(message, 1000),
            CorrelationId = _correlationContext.CorrelationId,
            AnonymousSessionId = await GetAnonymousSessionIdAsync(cancellationToken),
            AppVersion = _appVersion,
            Platform = await GetPlatformAsync(cancellationToken),
            IsOnline = isOnline,
            Route = SafeRoute(),
            ErrorType = exception?.GetType().Name,
            StackTrace = exception is null ? null : DiagnosticMetadata.Truncate(exception.StackTrace ?? string.Empty, 2000),
            Metadata = new Dictionary<string, string>(DiagnosticMetadata.Filter(metadata)),
            SyncStatus = SyncStatus.SavedLocally
        };
    }

    private async Task<Guid> GetAnonymousSessionIdAsync(CancellationToken cancellationToken)
    {
        if (_anonymousSessionId is { } existing)
        {
            return existing;
        }

        try
        {
            var stored = await _jsRuntime.InvokeAsync<string?>("fishingLogBookDiagnostics.getSessionId", cancellationToken);
            if (Guid.TryParse(stored, out var parsed) && parsed != Guid.Empty)
            {
                _anonymousSessionId = parsed;
                return parsed;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _logging.LogErrorAsync("diagnostic session read", exception, CancellationToken.None);
        }

        var created = Guid.NewGuid();
        _anonymousSessionId = created;
        try
        {
            await _jsRuntime.InvokeVoidAsync("fishingLogBookDiagnostics.setSessionId", cancellationToken, created.ToString("D"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _logging.LogErrorAsync("diagnostic session write", exception, CancellationToken.None);
        }

        return created;
    }

    private async Task<string?> GetPlatformAsync(CancellationToken cancellationToken)
    {
        try
        {
            return DiagnosticMetadata.Truncate(
                await _jsRuntime.InvokeAsync<string>("fishingLogBookDiagnostics.getPlatform", cancellationToken),
                120);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _logging.LogErrorAsync("diagnostic platform read", exception, CancellationToken.None);
            return null;
        }
    }

    private string SafeRoute()
    {
        try
        {
            return DiagnosticMetadata.Truncate(new Uri(_navigationManager.Uri).AbsolutePath, 80);
        }
        catch (Exception exception)
        {
            TryToLogError("diagnostic route read", exception);
            return string.Empty;
        }
    }

    private void TryToLogError(string source, Exception exception)
    {
        _ = _logging.LogErrorAsync(source, exception);
    }

    private async Task WriteConsoleAsync(
        DiagnosticLevel level,
        string eventName,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync(
                "fishingLogBookDiagnostics.console",
                cancellationToken,
                level.ToString(),
                eventName,
                message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _logging.LogErrorAsync("diagnostic console", exception, CancellationToken.None);
        }
    }
}
