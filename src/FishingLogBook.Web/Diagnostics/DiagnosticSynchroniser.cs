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
            var pending = await _store.GetPendingAsync(_config.MaxBatchSize, cancellationToken);
            if (pending.Count == 0)
            {
                _status.QueuedCount = await _store.GetCountAsync(cancellationToken);
                return;
            }

            var ready = pending.Where(item => item.RetryCount < _config.MaxUploadAttempts).ToArray();
            var expired = pending.Where(item => item.RetryCount >= _config.MaxUploadAttempts).Select(item => item.Id).ToArray();
            if (expired.Length > 0)
            {
                await _store.RemoveAsync(expired, cancellationToken);
            }

            if (ready.Length == 0)
            {
                _status.QueuedCount = await _store.GetCountAsync(cancellationToken);
                return;
            }

            try
            {
                await _client.UploadBatchAsync(ready.Select(ToDto).ToArray(), cancellationToken);
                await _store.RemoveAsync(ready.Select(item => item.Id).ToArray(), cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                _status.LastError = exception.GetType().Name;
                foreach (var item in ready)
                {
                    item.RetryCount++;
                    item.SyncStatus = SyncStatus.FailedToSynchronise;
                    await _store.SaveAsync(item, cancellationToken);
                }
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                _status.LastError = exception.GetType().Name;
                foreach (var item in ready)
                {
                    item.RetryCount++;
                    item.SyncStatus = SyncStatus.FailedToSynchronise;
                    await _store.SaveAsync(item, cancellationToken);
                }
            }

            _status.QueuedCount = await _store.GetCountAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _status.LastError = exception.GetType().Name;
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
