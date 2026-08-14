using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Diagnostics;

public sealed class DiagnosticLogger : IDiagnosticLogger
{
    private static readonly AsyncLocal<bool> IsWriting = new();

    private readonly IDiagnosticEventStore _store;
    private readonly DiagnosticStatus _status;
    private readonly DiagnosticsClientConfig _config;
    private readonly CorrelationContext _correlationContext;
    private readonly INetworkStatus _networkStatus;
    private readonly NavigationManager _navigationManager;
    private readonly IJSRuntime _jsRuntime;
    private readonly DiagnosticLevel _minimumPersistLevel;
    private readonly string _appVersion;
    private Guid? _anonymousSessionId;

    public DiagnosticLogger(
        IDiagnosticEventStore store,
        DiagnosticStatus status,
        DiagnosticsClientConfig config,
        CorrelationContext correlationContext,
        INetworkStatus networkStatus,
        NavigationManager navigationManager,
        IJSRuntime jsRuntime)
    {
        _store = store;
        _status = status;
        _config = config;
        _correlationContext = correlationContext;
        _networkStatus = networkStatus;
        _navigationManager = navigationManager;
        _jsRuntime = jsRuntime;
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
            finally
            {
                IsWriting.Value = false;
            }
        }
        catch (Exception exceptionWhileLogging)
        {
            _status.LastError = exceptionWhileLogging.GetType().Name;
            await WriteConsoleAsync(DiagnosticLevel.Error, eventName, "Diagnostic persistence failed.", cancellationToken);
        }
    }

    private async Task<DiagnosticEvent> CreateEventAsync(
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
        catch
        {
            // Online state is optional on the diagnostic event.
        }

        return new DiagnosticEvent
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
        catch
        {
            // Fall through and create a session locally.
        }

        var created = Guid.NewGuid();
        _anonymousSessionId = created;
        try
        {
            await _jsRuntime.InvokeVoidAsync("fishingLogBookDiagnostics.setSessionId", cancellationToken, created.ToString("D"));
        }
        catch
        {
            // Session id still works for this in-memory lifetime.
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
        catch
        {
            return null;
        }
    }

    private string SafeRoute()
    {
        try
        {
            return DiagnosticMetadata.Truncate(new Uri(_navigationManager.Uri).AbsolutePath, 80);
        }
        catch
        {
            return string.Empty;
        }
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
        catch
        {
            // Console fallback must never throw.
        }
    }
}
