using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Shared.Dtos;
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
    private readonly ICatchStore _catchStore;
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
        ICatchStore catchStore,
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
        _catchStore = catchStore;
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
            _activeEdits.EditReleased += OnTrigger;
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
            _activeEdits.EditReleased -= OnTrigger;
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

            deletedAny |= await _activeEdits.RunIfInactiveAsync(candidate.CatchId,
                async () => _sessionCoordinator.IsCurrent(lease)
                    && await _store.TryDeleteAsync(candidate, _timeProvider.GetUtcNow(), cancellationToken), cancellationToken);
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

        var local = await _catchStore.GetForOwnerAsync(candidate.UserId, candidate.CatchId, cancellationToken);
        var view = remote.Value!;
        var photographs = view.Photographs;
        return local is not null && local.Revision == candidate.Revision
            && MatchesCatch(local, view)
            && photographs.Count == candidate.PhotographIds.Count
            && photographs.All(photograph => candidate.PhotographIds.Contains(photograph.Id)
                && !string.IsNullOrWhiteSpace(photograph.Url));
    }

    private static bool MatchesCatch(LocalCatchRecordModel local, CatchViewDto remote)
    {
        return (local.Id, local.CaughtByUserId, local.RecordedByUserId, local.TripId)
                == (remote.Id, remote.CaughtByUserId, remote.RecordedByUserId, remote.TripId)
            && SameStoredInstant(local.CaughtOn, remote.CaughtOn)
            && (local.SpeciesName, (decimal?)local.Weight, (decimal?)local.Length, local.Method, local.BaitOrLure, local.Notes, local.PlaceName)
                == (remote.SpeciesName, remote.Weight, remote.Length, remote.Method, remote.BaitOrLure, remote.Notes, remote.PlaceName)
            && MatchesLocation(local, remote);
    }

    private static bool MatchesLocation(LocalCatchRecordModel local, CatchViewDto remote)
    {
        if (local.Location is not { } location)
        {
            return true;
        }

        return remote.Location is { Mode: LocationDefaults.ExposureExact } exposure
            && (location.Latitude, location.Longitude, location.AccuracyMetres, location.Source, location.Visibility)
                == (exposure.Latitude, exposure.Longitude, exposure.AccuracyMetres, exposure.Source, exposure.Visibility)
            && exposure.CapturedOn is { } capturedOn && SameStoredInstant(location.CapturedOn, capturedOn);
    }

    private static bool SameStoredInstant(DateTimeOffset local, DateTimeOffset remote)
    {
        return Math.Abs((local - remote).Ticks) <= TimeSpan.TicksPerMicrosecond;
    }

    private async Task DeletePendingFilesAsync(Guid userId, CancellationToken cancellationToken)
    {
        foreach (var (id, relativePath) in await _store.ListPendingFileDeletionsAsync(userId, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _activeEdits.RunIfInactiveAsync(null, async () =>
            {
                await _store.TryDeleteFileAsync(userId, id, relativePath,
                    () => _fileStore.TryDelete(userId, relativePath), cancellationToken);
                return true;
            }, cancellationToken);
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
