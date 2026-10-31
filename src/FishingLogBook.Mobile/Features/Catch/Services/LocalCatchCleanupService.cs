using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Profile.Services;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Catch.Services;

public sealed class LocalCatchCleanupService : ILocalCatchCleanupService, IDisposable
{
    private readonly object _stateLock = new();
    private readonly ILocalCatchCleanupStore _store;
    private readonly ICatchRemoteClient _remoteClient;
    private readonly IPhotographFileStore _fileStore;
    private readonly IActiveCatchEditRegistry _activeEdits;
    private readonly IAccountSessionCoordinator _sessionCoordinator;
    private readonly IConnectivityService _connectivity;
    private readonly IOutboxChangeNotifier _notifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LocalCatchCleanupService> _logger;
    private bool _running;
    private bool _rerunRequested;
    private bool _started;
    private bool _disposed;

    public LocalCatchCleanupService(
        ILocalCatchCleanupStore store,
        ICatchRemoteClient remoteClient,
        IPhotographFileStore fileStore,
        IActiveCatchEditRegistry activeEdits,
        IAccountSessionCoordinator sessionCoordinator,
        IConnectivityService connectivity,
        IOutboxChangeNotifier notifier,
        TimeProvider timeProvider,
        ILogger<LocalCatchCleanupService> logger)
    {
        _store = store;
        _remoteClient = remoteClient;
        _fileStore = fileStore;
        _activeEdits = activeEdits;
        _sessionCoordinator = sessionCoordinator;
        _connectivity = connectivity;
        _notifier = notifier;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public void Start()
    {
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
            {
                return;
            }

            _started = true;
            _notifier.SyncRunCompleted += OnTrigger;
            _sessionCoordinator.SessionActivated += OnTrigger;
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            if (_running)
            {
                _rerunRequested = true;
                return;
            }

            _running = true;
        }

        var again = true;
        while (again)
        {
            try
            {
                await CleanAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Local catch cleanup was cancelled.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Local catch cleanup failed.");
            }

            lock (_stateLock)
            {
                again = _rerunRequested && !cancellationToken.IsCancellationRequested && !_disposed;
                _rerunRequested = false;
                if (!again)
                {
                    _running = false;
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _notifier.SyncRunCompleted -= OnTrigger;
            _sessionCoordinator.SessionActivated -= OnTrigger;
        }
    }

    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        var lease = _sessionCoordinator.Capture();
        if (lease is null)
        {
            return;
        }

        await DeletePendingFilesAsync(lease.UserId, cancellationToken);
        if (!_connectivity.IsOnline)
        {
            return;
        }

        var deletedAny = false;
        foreach (var candidate in await _store.ListCandidatesAsync(lease.UserId, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_sessionCoordinator.IsCurrent(lease) || !_connectivity.IsOnline)
            {
                break;
            }

            if (_activeEdits.IsActive(candidate.CatchId)
                || !await IsConfirmedOnServerAsync(candidate, cancellationToken)
                || _activeEdits.IsActive(candidate.CatchId))
            {
                continue;
            }

            deletedAny |= await _store.TryDeleteAsync(candidate, _timeProvider.GetUtcNow(), cancellationToken);
        }

        await DeletePendingFilesAsync(lease.UserId, cancellationToken);
        if (deletedAny)
        {
            _notifier.NotifyCatchesChanged();
        }
    }

    private async Task<bool> IsConfirmedOnServerAsync(
        LocalCatchCleanupCandidateModel candidate,
        CancellationToken cancellationToken)
    {
        var remote = await _remoteClient.GetAsync(candidate.CatchId, cancellationToken);
        if (!remote.IsSuccess)
        {
            return false;
        }

        var photographs = remote.Value!.Photographs;
        return photographs.Count == candidate.PhotographIds.Count
            && photographs.All(photograph => candidate.PhotographIds.Contains(photograph.Id)
                && !string.IsNullOrWhiteSpace(photograph.Url));
    }

    private async Task DeletePendingFilesAsync(Guid userId, CancellationToken cancellationToken)
    {
        foreach (var (id, relativePath) in await _store.ListPendingFileDeletionsAsync(userId, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _store.IsFileReferencedAsync(userId, relativePath, cancellationToken)
                || _fileStore.TryDelete(userId, relativePath))
            {
                await _store.CompleteFileDeletionAsync(id, cancellationToken);
            }
        }
    }

    private void OnTrigger(object? sender, EventArgs args)
    {
        Observe(RunAsync(CancellationToken.None));
    }

    private async void Observe(Task operation)
    {
        try
        {
            await operation;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Local catch cleanup trigger could not be processed.");
        }
    }
}
