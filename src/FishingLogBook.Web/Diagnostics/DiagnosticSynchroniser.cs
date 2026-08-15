using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Services;

namespace FishingLogBook.Web.Diagnostics;

public sealed class DiagnosticSynchroniser : IDiagnosticSynchroniser
{
    private readonly IDiagnosticEventStore _store;
    private readonly IDiagnosticClient _client;
    private readonly INetworkStatus _networkStatus;
    private readonly DiagnosticStatus _status;
    private readonly DiagnosticsClientConfig _config;

    public DiagnosticSynchroniser(
        IDiagnosticEventStore store,
        IDiagnosticClient client,
        INetworkStatus networkStatus,
        DiagnosticStatus status,
        DiagnosticsClientConfig config)
    {
        _store = store;
        _client = client;
        _networkStatus = networkStatus;
        _status = status;
        _config = config;
    }

    public async Task SynchronisePendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await _networkStatus.IsOnlineAsync(cancellationToken))
            {
                _status.IsOnline = false;
                return;
            }

            _status.IsOnline = true;
            await DrainQueueAsync(cancellationToken);
            _status.QueuedCount = await _store.GetCountAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _status.LastError = exception.GetType().Name;
        }
    }

    private async Task DrainQueueAsync(CancellationToken cancellationToken)
    {
        var maxBatches = Math.Max(1, _config.MaxQueueSize / Math.Max(1, _config.MaxBatchSize));
        var previousCount = int.MaxValue;
        for (var batch = 0; batch < maxBatches; batch++)
        {
            var count = await _store.GetCountAsync(cancellationToken);
            if (count == 0 || count >= previousCount)
            {
                return;
            }

            previousCount = count;
            if (!await TryUploadNextBatchAsync(cancellationToken))
            {
                return;
            }
        }
    }

    private async Task<bool> TryUploadNextBatchAsync(CancellationToken cancellationToken)
    {
        var pending = await _store.GetPendingAsync(_config.MaxBatchSize, cancellationToken);
        if (pending.Count == 0)
        {
            return false;
        }

        var expiredIds = pending
            .Where(item => item.RetryCount >= _config.MaxUploadAttempts)
            .Select(item => item.Id)
            .ToArray();
        if (expiredIds.Length > 0)
        {
            await _store.RemoveAsync(expiredIds, cancellationToken);
        }

        var ready = pending.Where(item => item.RetryCount < _config.MaxUploadAttempts).ToArray();
        if (ready.Length == 0)
        {
            return false;
        }

        try
        {
            await _client.UploadBatchAsync(ready.Select(ToDto).ToArray(), cancellationToken);
            await _store.RemoveAsync(ready.Select(item => item.Id).ToArray(), cancellationToken);
            return true;
        }
        catch (HttpRequestException exception)
        {
            await MarkFailedAsync(ready, exception, cancellationToken);
            return false;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            await MarkFailedAsync(ready, exception, cancellationToken);
            return false;
        }
    }

    private async Task MarkFailedAsync(
        IReadOnlyList<DiagnosticEvent> ready,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _status.LastError = exception.GetType().Name;
        foreach (var item in ready)
        {
            item.RetryCount++;
            item.SyncStatus = SyncStatus.FailedToSynchronise;
            await _store.SaveAsync(item, cancellationToken);
        }
    }

    private static ClientDiagnosticEventDto ToDto(DiagnosticEvent diagnosticEvent)
    {
        return new ClientDiagnosticEventDto
        {
            Id = diagnosticEvent.Id,
            TimestampUtc = diagnosticEvent.TimestampUtc,
            Level = diagnosticEvent.Level.ToString(),
            EventName = diagnosticEvent.EventName,
            Message = diagnosticEvent.Message,
            CorrelationId = diagnosticEvent.CorrelationId,
            AnonymousSessionId = diagnosticEvent.AnonymousSessionId,
            AppVersion = diagnosticEvent.AppVersion,
            Platform = diagnosticEvent.Platform,
            IsOnline = diagnosticEvent.IsOnline,
            Route = diagnosticEvent.Route,
            ErrorType = diagnosticEvent.ErrorType,
            StackTrace = diagnosticEvent.StackTrace,
            Metadata = diagnosticEvent.Metadata
        };
    }
}
