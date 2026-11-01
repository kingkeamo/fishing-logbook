using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Stores;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public sealed class LocalTripCleanupService : ILocalTripCleanupService, IDisposable
{
    private readonly object _stateLock = new();
    private readonly ILocalTripCleanupStore _store;
    private readonly ITripStore _trips;
    private readonly ITripClient _client;
    private readonly IPhotographFileStore _fileStore;
    private readonly IActiveTripEditRegistry _activeEdits;
    private readonly IAccountSessionCoordinator _sessionCoordinator;
    private readonly IConnectivityService _connectivity;
    private readonly IOutboxChangeNotifier _notifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LocalTripCleanupService> _logger;
    private bool _running;
    private bool _rerunRequested;
    private bool _started;
    private bool _disposed;

    public LocalTripCleanupService(
        ILocalTripCleanupStore store,
        ITripStore trips,
        ITripClient client,
        IPhotographFileStore fileStore,
        IActiveTripEditRegistry activeEdits,
        IAccountSessionCoordinator sessionCoordinator,
        IConnectivityService connectivity,
        IOutboxChangeNotifier notifier,
        TimeProvider timeProvider,
        ILogger<LocalTripCleanupService> logger)
    {
        _store = store;
        _trips = trips;
        _client = client;
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
                _logger.LogDebug("Local trip cleanup was cancelled.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Local trip cleanup failed.");
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

            if (_activeEdits.IsActive(candidate.TripId)
                || !await IsConfirmedOnServerAsync(candidate, cancellationToken)
                || _activeEdits.IsActive(candidate.TripId))
            {
                continue;
            }

            deletedAny |= await _activeEdits.RunIfInactiveAsync(candidate.TripId,
                async () => _sessionCoordinator.IsCurrent(lease)
                    && await _store.TryDeleteAsync(candidate, _timeProvider.GetUtcNow(), cancellationToken), cancellationToken);
        }

        await DeletePendingFilesAsync(lease.UserId, cancellationToken);
        if (deletedAny)
        {
            _notifier.NotifyStateChanged();
        }
    }

    private async Task<bool> IsConfirmedOnServerAsync(
        LocalTripCleanupCandidateModel candidate,
        CancellationToken cancellationToken)
    {
        var remote = await _client.GetAsync(candidate.TripId, cancellationToken);
        if (!remote.IsSuccess)
        {
            return false;
        }

        var local = await _trips.GetAsync(candidate.UserId, candidate.TripId, cancellationToken);
        return local is not null
            && local.Revision == candidate.Revision
            && MatchesTrip(local, remote.Value!);
    }

    private static bool MatchesTrip(LocalTripRecordModel local, TripDetailDto remote)
    {
        var trip = local.Detail.Trip;
        return trip.Id == remote.Trip.Id
            && trip.OwnerUserId == remote.Trip.OwnerUserId
            && trip.Status == remote.Trip.Status
            && SameStoredInstant(trip.StartedOn, remote.Trip.StartedOn)
            && SameOptionalInstant(trip.EndedOn, remote.Trip.EndedOn)
            && SameOptionalInstant(trip.PlannedStartOn, remote.Trip.PlannedStartOn)
            && trip.Title == remote.Trip.Title
            && trip.PlaceName == remote.Trip.PlaceName
            && MatchesNotes(local.Detail.Notes, remote.Notes)
            && MatchesPhotographs(local.Detail.Photographs, remote.Photographs);
    }

    private static bool MatchesNotes(IReadOnlyList<TripNoteDto> local, IReadOnlyList<TripNoteDto> remote)
    {
        return local.Count == remote.Count
            && remote.All(note => local.Any(item => item.Id == note.Id));
    }

    private static bool MatchesPhotographs(
        IReadOnlyList<TripPhotographViewDto> local,
        IReadOnlyList<TripPhotographViewDto> remote)
    {
        return local.Count == remote.Count
            && remote.All(photograph => local.Any(item => item.Id == photograph.Id)
                && !string.IsNullOrWhiteSpace(photograph.Url));
    }

    private static bool SameOptionalInstant(DateTimeOffset? local, DateTimeOffset? remote)
    {
        return local is { } left && remote is { } right
            ? SameStoredInstant(left, right)
            : local is null && remote is null;
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
            _logger.LogWarning(exception, "Local trip cleanup trigger could not be processed.");
        }
    }
}
