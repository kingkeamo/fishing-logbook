using System.Collections.Concurrent;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Offline.Dependencies;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;

namespace FishingLogBook.Web.Features.Trips.Offline.Synchronisers;

public sealed class TripPhotographSynchroniser : ITripPhotographSynchroniser
{
    private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();
    private readonly ITripPhotographStore _store;
    private readonly ITripDependencyService _tripDependency;
    private readonly ITripClient _client;
    private readonly INetworkService _networkService;
    private readonly IDiagnosticLogger _diagnostics;
    private readonly ILoggingService _logging;

    public TripPhotographSynchroniser(
        ITripPhotographStore store,
        ITripDependencyService tripDependency,
        ITripClient client,
        INetworkService networkService,
        IDiagnosticLogger diagnostics,
        ILoggingService logging)
    {
        _store = store;
        _tripDependency = tripDependency;
        _client = client;
        _networkService = networkService;
        _diagnostics = diagnostics;
        _logging = logging;
    }

    public async Task SynchronisePendingAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        if (ownerUserId == Guid.Empty)
        {
            return;
        }

        IReadOnlyList<TripPhotographModel> pending =
            (await _store.GetPendingAsync(ownerUserId, cancellationToken))
            .Where(NeedsAutomaticSynchronisation)
            .ToArray();
        if (pending.Count == 0 || !await _networkService.IsOnlineAsync(cancellationToken))
        {
            return;
        }

        var readiness = await ResolveTripReadinessAsync(ownerUserId, pending, cancellationToken);
        foreach (var photograph in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!readiness[photograph.TripId])
            {
                await SafeLogAsync(
                    DiagnosticLevel.Information,
                    DiagnosticEventNames.TripPhotoSyncWaitingForTrip,
                    "Trip photograph is waiting for its trip to reach the server.",
                    photograph,
                    exception: null,
                    cancellationToken);
                continue;
            }

            await SynchroniseGuardedAsync(ownerUserId, photograph, cancellationToken);
        }
    }

    private async Task<IReadOnlyDictionary<Guid, bool>> ResolveTripReadinessAsync(
        Guid ownerUserId,
        IReadOnlyList<TripPhotographModel> pending,
        CancellationToken cancellationToken)
    {
        var readiness = new Dictionary<Guid, bool>();
        foreach (var tripId in pending.Select(photograph => photograph.TripId))
        {
            if (readiness.ContainsKey(tripId))
            {
                continue;
            }

            readiness[tripId] = await _tripDependency.IsTripReadyForServerAsync(
                ownerUserId,
                tripId,
                cancellationToken);
        }

        return readiness;
    }

    private async Task SynchroniseGuardedAsync(
        Guid ownerUserId,
        TripPhotographModel photograph,
        CancellationToken cancellationToken)
    {
        if (!_inFlight.TryAdd(photograph.Id, 0))
        {
            return;
        }

        try
        {
            await SynchronisePhotographAsync(ownerUserId, photograph, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await SafeLogAsync(
                DiagnosticLevel.Error,
                DiagnosticEventNames.TripPhotoSyncFailed,
                "Trip photograph synchronisation failed.",
                photograph,
                exception,
                cancellationToken);
            await MarkFailedAsync(ownerUserId, photograph, cancellationToken);
        }
        finally
        {
            _inFlight.TryRemove(photograph.Id, out _);
        }
    }

    private async Task MarkFailedAsync(
        Guid ownerUserId,
        TripPhotographModel photograph,
        CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await _store.GetBytesAsync(
                ownerUserId,
                photograph.TripId,
                photograph.Id,
                cancellationToken);
            if (bytes is not { Length: > 0 })
            {
                return;
            }

            await _store.SaveAsync(
                photograph with
                {
                    Bytes = bytes,
                    SyncStatus = SyncStatus.FailedToSynchronise
                },
                cancellationToken);
        }
        catch (Exception storeException) when (storeException is not OperationCanceledException)
        {
            await _logging.LogErrorAsync(
                "recording a failed trip photograph synchronisation",
                storeException,
                CancellationToken.None);
        }
    }

    private static bool NeedsAutomaticSynchronisation(TripPhotographModel photograph)
    {
        return photograph.SyncStatus != SyncStatus.FailedToSynchronise;
    }

    private async Task SynchronisePhotographAsync(
        Guid ownerUserId,
        TripPhotographModel photograph,
        CancellationToken cancellationToken)
    {
        await SafeLogAsync(
            DiagnosticLevel.Information,
            DiagnosticEventNames.TripPhotoSyncStarted,
            "Trip photograph synchronisation started.",
            photograph,
            exception: null,
            cancellationToken);

        var bytes = await _store.GetBytesAsync(
            ownerUserId,
            photograph.TripId,
            photograph.Id,
            cancellationToken);
        if (bytes is not { Length: > 0 })
        {
            return;
        }

        var upload = await _client.CreatePhotographUploadAsync(
            photograph.TripId,
            new PhotographUploadRequestDto(photograph.Id, photograph.ContentType),
            cancellationToken);
        await _client.UploadPhotographAsync(
            upload.UploadUrl,
            bytes,
            photograph.ContentType,
            cancellationToken);
        await _client.RecordPhotographAsync(
            photograph.TripId,
            new RecordTripPhotographDto(
                photograph.Id,
                upload.ObjectKey,
                photograph.ContentType,
                photograph.AddedOn,
                photograph.CapturedOn),
            cancellationToken);

        var current = await _store.GetBytesAsync(
            ownerUserId,
            photograph.TripId,
            photograph.Id,
            cancellationToken);
        if (current is not { Length: > 0 })
        {
            return;
        }

        await _store.SaveAsync(
            photograph with
            {
                Bytes = current,
                ObjectKey = upload.ObjectKey,
                SyncStatus = SyncStatus.Synchronised,
                SyncedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);
        await SafeLogAsync(
            DiagnosticLevel.Information,
            DiagnosticEventNames.TripPhotoSyncCompleted,
            "Trip photograph synchronisation completed.",
            photograph,
            exception: null,
            cancellationToken);
    }

    private async Task SafeLogAsync(
        DiagnosticLevel level,
        string eventName,
        string message,
        TripPhotographModel photograph,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>
        {
            [DiagnosticMetadata.TripId] = photograph.TripId.ToString("D"),
            [DiagnosticMetadata.PhotographId] = photograph.Id.ToString("D")
        };
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
                    "trip photograph diagnostic",
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
