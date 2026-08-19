using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Clients;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.Diagnostics.Storage.Stores;

namespace FishingLogBook.Web.Features.Diagnostics.Synchronisers;

public sealed class DiagnosticSynchroniser : IDiagnosticSynchroniser
{
    private readonly IDiagnosticEventStore _store;
    private readonly IDiagnosticClient _client;
    private readonly INetworkService _networkStatus;
    private readonly DiagnosticStatusModel _status;
    private readonly DiagnosticsClientConfig _config;

    public DiagnosticSynchroniser(
        IDiagnosticEventStore store,
        IDiagnosticClient client,
        INetworkService networkStatus,
        DiagnosticStatusModel status,
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
            var isOnline = await RunAsync(
                DiagnosticOperations.NetworkCheck,
                () => _networkStatus.IsOnlineAsync(cancellationToken));
            if (!isOnline)
            {
                _status.IsOnline = false;
                return;
            }

            _status.IsOnline = true;
            await DrainQueueAsync(cancellationToken);
            var count = await RunAsync(
                DiagnosticOperations.QueueCount,
                () => _store.GetCountAsync(cancellationToken));
            _status.RecordQueueCount(count);
        }
        catch (Exception)
        {
            if (_status.LastOperation == DiagnosticOperations.QueueCount)
            {
                _status.MarkQueueCountUnavailable();
            }
        }
    }

    private async Task DrainQueueAsync(CancellationToken cancellationToken)
    {
        var maxBatches = Math.Max(1, _config.MaxQueueSize / Math.Max(1, _config.MaxBatchSize));
        var previousCount = int.MaxValue;
        for (var batch = 0; batch < maxBatches; batch++)
        {
            var count = await RunAsync(
                DiagnosticOperations.QueueCount,
                () => _store.GetCountAsync(cancellationToken));
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
        var pending = await RunAsync(
            DiagnosticOperations.QueueRead,
            () => _store.GetPendingAsync(_config.MaxBatchSize, cancellationToken));
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
            await RunAsync(
                DiagnosticOperations.QueueDelete,
                () => _store.RemoveAsync(expiredIds, cancellationToken));
        }

        var ready = pending.Where(item => item.RetryCount < _config.MaxUploadAttempts).ToArray();
        if (ready.Length == 0)
        {
            return false;
        }

        try
        {
            await RunAsync(
                DiagnosticOperations.Upload,
                () => _client.UploadBatchAsync(ready.Select(ToDto).ToArray(), cancellationToken));
            await RunAsync(
                DiagnosticOperations.QueueDelete,
                () => _store.RemoveAsync(ready.Select(item => item.Id).ToArray(), cancellationToken));
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
        IReadOnlyList<DiagnosticEventModel> ready,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _status.RecordFailure(DiagnosticOperations.Upload, exception);
        foreach (var item in ready)
        {
            item.RetryCount++;
            item.SyncStatus = SyncStatus.FailedToSynchronise;
            await RunAsync(
                DiagnosticOperations.FailedEventSave,
                () => _store.SaveAsync(item, cancellationToken));
        }
    }

    private async Task RunAsync(string operation, Func<Task> action)
    {
        await RunAsync(operation, async () =>
        {
            await action();
            return 0;
        });
    }

    private async Task<T> RunAsync<T>(string operation, Func<Task<T>> action)
    {
        _status.RecordSuccess(operation);
        try
        {
            return await action();
        }
        catch (Exception exception)
        {
            _status.RecordFailure(operation, exception);
            throw;
        }
    }

    private static ClientDiagnosticEventDto ToDto(DiagnosticEventModel diagnosticEvent)
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
