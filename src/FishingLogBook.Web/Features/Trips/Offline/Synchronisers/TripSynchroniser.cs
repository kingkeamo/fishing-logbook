using System.Collections.Concurrent;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;

namespace FishingLogBook.Web.Features.Trips.Offline.Synchronisers;

public sealed class TripSynchroniser : ITripSynchroniser
{
    private static readonly TimeSpan SyncedCacheRetentionWindow = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();
    private readonly ITripStore _store;
    private readonly ITripClient _client;
    private readonly INetworkService _networkService;
    private readonly IActiveTripService _activeTripService;
    private readonly IDiagnosticLogger _diagnostics;
    private readonly ILoggingService _logging;

    public TripSynchroniser(
        ITripStore store,
        ITripClient client,
        INetworkService networkService,
        IActiveTripService activeTripService,
        IDiagnosticLogger diagnostics,
        ILoggingService logging)
    {
        _store = store;
        _client = client;
        _networkService = networkService;
        _activeTripService = activeTripService;
        _diagnostics = diagnostics;
        _logging = logging;
    }

    public async Task SynchronisePendingAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        if (ownerUserId == Guid.Empty)
        {
            return;
        }

        var pending = await _store.GetPendingAsync(ownerUserId, cancellationToken);
        if (pending.Count == 0 || !await _networkService.IsOnlineAsync(cancellationToken))
        {
            return;
        }

        var mutated = false;
        foreach (var trip in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mutated |= await SynchroniseGuardedAsync(ownerUserId, trip, cancellationToken);
        }

        if (mutated)
        {
            _activeTripService.Invalidate();
        }
    }

    public async Task CleanupSyncedCacheAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        if (ownerUserId == Guid.Empty || !await _networkService.IsOnlineAsync(cancellationToken))
        {
            return;
        }

        try
        {
            await SafeLogAsync(
                DiagnosticLevel.Debug,
                DiagnosticEventNames.TripCacheCleanupStarted,
                "Synced local trip cache cleanup started.",
                tripId: null,
                exception: null,
                cancellationToken);
            var removed = await _store.CleanupSyncedAsync(
                ownerUserId,
                DateTimeOffset.UtcNow - SyncedCacheRetentionWindow,
                cancellationToken);
            if (removed > 0)
            {
                _activeTripService.Invalidate();
            }

            await SafeLogAsync(
                DiagnosticLevel.Debug,
                DiagnosticEventNames.TripCacheCleanupCompleted,
                "Synced local trip cache cleanup completed.",
                tripId: null,
                exception: null,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await SafeLogAsync(
                DiagnosticLevel.Warning,
                DiagnosticEventNames.TripCacheCleanupFailed,
                "Synced local trip cache cleanup failed.",
                tripId: null,
                exception,
                cancellationToken);
        }
    }

    private async Task<bool> SynchroniseGuardedAsync(
        Guid ownerUserId,
        TripModel trip,
        CancellationToken cancellationToken)
    {
        if (!_inFlight.TryAdd(trip.Id, 0))
        {
            return false;
        }

        try
        {
            return await SynchroniseTripAsync(ownerUserId, trip, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await MarkFailedAsync(ownerUserId, trip, exception, cancellationToken);
            return true;
        }
        finally
        {
            _inFlight.TryRemove(trip.Id, out _);
        }
    }

    private async Task<bool> SynchroniseTripAsync(
        Guid ownerUserId,
        TripModel trip,
        CancellationToken cancellationToken)
    {
        await SafeLogAsync(
            DiagnosticLevel.Information,
            DiagnosticEventNames.TripSyncStarted,
            "Trip synchronisation started.",
            trip.Id,
            exception: null,
            cancellationToken);

        var sent = ToDto(trip);
        var accepted = await _client.UpsertAsync(sent, cancellationToken);
        var current = await _store.GetAsync(ownerUserId, trip.Id, cancellationToken);
        if (current is null)
        {
            return false;
        }

        if (!HasSameSynchronisedContent(current, sent))
        {
            await SafeLogAsync(
                DiagnosticLevel.Information,
                DiagnosticEventNames.TripSyncCompleted,
                "Trip changed locally while synchronising and stays pending.",
                trip.Id,
                exception: null,
                cancellationToken);
            return false;
        }

        var reconciled = ApplyServerLifecycle(current, accepted);
        if (reconciled.Status != current.Status)
        {
            await SafeLogAsync(
                DiagnosticLevel.Warning,
                DiagnosticEventNames.TripActiveReconciled,
                "The server completed this trip because another trip started later.",
                trip.Id,
                exception: null,
                cancellationToken);
        }

        await _store.SaveAsync(
            reconciled with
            {
                SyncStatus = SyncStatus.Synchronised,
                SyncedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);
        await SafeLogAsync(
            DiagnosticLevel.Information,
            DiagnosticEventNames.TripSyncCompleted,
            "Trip synchronisation completed.",
            trip.Id,
            exception: null,
            cancellationToken);
        return true;
    }

    private async Task MarkFailedAsync(
        Guid ownerUserId,
        TripModel trip,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await SafeLogAsync(
            DiagnosticLevel.Error,
            DiagnosticEventNames.TripSyncFailed,
            "Trip synchronisation failed.",
            trip.Id,
            exception,
            cancellationToken);
        try
        {
            var current = await _store.GetAsync(ownerUserId, trip.Id, cancellationToken);
            if (current is null || current.SyncStatus == SyncStatus.Synchronised)
            {
                return;
            }

            await _store.SaveAsync(
                current with { SyncStatus = SyncStatus.FailedToSynchronise },
                cancellationToken);
        }
        catch (Exception storeException) when (storeException is not OperationCanceledException)
        {
            await _logging.LogErrorAsync(
                "recording a failed trip synchronisation",
                storeException,
                CancellationToken.None);
        }
    }

    private static TripModel ApplyServerLifecycle(TripModel current, TripDto? accepted)
    {
        if (accepted is null)
        {
            return current;
        }

        return current with
        {
            Status = accepted.Status,
            EndedOn = accepted.EndedOn
        };
    }

    private static bool HasSameSynchronisedContent(TripModel current, TripDto sent)
    {
        return current.Status == sent.Status
            && current.StartedOn == sent.StartedOn
            && current.EndedOn == sent.EndedOn
            && string.Equals(current.Title, sent.Title, StringComparison.Ordinal)
            && string.Equals(current.PlaceName, sent.PlaceName, StringComparison.Ordinal)
            && HasSameLocation(current, sent);
    }

    private static bool HasSameLocation(TripModel current, TripDto sent)
    {
        if (current.Location is null || sent.Location is null)
        {
            return current.Location is null && sent.Location is null;
        }

        return current.Location.Latitude == sent.Location.Latitude
            && current.Location.Longitude == sent.Location.Longitude
            && current.Location.AccuracyMetres == sent.Location.AccuracyMetres
            && current.Location.CapturedOn == sent.Location.CapturedOn
            && string.Equals(current.Location.Source, sent.Location.Source, StringComparison.Ordinal)
            && string.Equals(current.Location.Visibility, sent.Location.Visibility, StringComparison.Ordinal)
            && string.Equals(
                current.Location.ConsentVersion,
                sent.Location.ConsentVersion,
                StringComparison.Ordinal);
    }

    private static TripDto ToDto(TripModel trip)
    {
        return new TripDto(
            trip.Id,
            trip.Status,
            trip.StartedOn,
            trip.EndedOn,
            trip.Location is null
                ? null
                : new TripLocationDto(
                    trip.Location.Latitude,
                    trip.Location.Longitude,
                    trip.Location.AccuracyMetres,
                    trip.Location.CapturedOn,
                    trip.Location.Source,
                    trip.Location.Visibility,
                    trip.Location.ConsentVersion))
        {
            Title = trip.Title,
            PlaceName = trip.PlaceName
        };
    }

    private async Task SafeLogAsync(
        DiagnosticLevel level,
        string eventName,
        string message,
        Guid? tripId,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>();
        if (tripId is not null)
        {
            metadata[DiagnosticMetadata.TripId] = tripId.Value.ToString("D");
        }

        if (exception is not null)
        {
            metadata[DiagnosticMetadata.ErrorType] = exception.GetType().Name;
        }

        try
        {
            await _diagnostics.LogAsync(level, eventName, message, metadata, cancellationToken: cancellationToken);
        }
        catch (Exception loggingException)
        {
            try
            {
                await _logging.LogErrorAsync(
                    "trip synchronisation diagnostic",
                    loggingException,
                    CancellationToken.None);
            }
            catch (Exception)
            {
                // Diagnostics never control synchronisation state.
            }
        }
    }
}
