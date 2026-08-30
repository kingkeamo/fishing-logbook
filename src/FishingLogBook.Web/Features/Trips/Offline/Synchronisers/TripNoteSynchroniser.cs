using System.Collections.Concurrent;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Offline.Dependencies;
using FishingLogBook.Web.Common.Offline.Synchronisers;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;

namespace FishingLogBook.Web.Features.Trips.Offline.Synchronisers;

public sealed class TripNoteSynchroniser : ITripNoteSynchroniser
{
    private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();
    private readonly ITripNoteStore _store;
    private readonly ITripDependencyService _tripDependency;
    private readonly ITripClient _client;
    private readonly INetworkService _networkService;
    private readonly IDiagnosticLogger _diagnostics;
    private readonly ILoggingService _logging;

    public TripNoteSynchroniser(
        ITripNoteStore store,
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

        IReadOnlyList<TripNoteModel> pending = (await _store.GetPendingAsync(ownerUserId, cancellationToken))
            .Where(NeedsAutomaticSynchronisation)
            .ToArray();
        if (pending.Count == 0 || !await _networkService.IsOnlineAsync(cancellationToken))
        {
            return;
        }

        var readiness = await ResolveTripReadinessAsync(ownerUserId, pending, cancellationToken);
        foreach (var note in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!readiness[note.TripId])
            {
                await SafeLogAsync(
                    DiagnosticLevel.Information,
                    DiagnosticEventNames.TripNoteSyncWaitingForTrip,
                    "Trip note is waiting for its trip to reach the server.",
                    note,
                    exception: null,
                    cancellationToken);
                continue;
            }

            await SynchroniseGuardedAsync(ownerUserId, note, cancellationToken);
        }
    }

    private async Task<IReadOnlyDictionary<Guid, bool>> ResolveTripReadinessAsync(
        Guid ownerUserId,
        IReadOnlyList<TripNoteModel> pending,
        CancellationToken cancellationToken)
    {
        var readiness = new Dictionary<Guid, bool>();
        foreach (var tripId in pending.Select(note => note.TripId))
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
        TripNoteModel note,
        CancellationToken cancellationToken)
    {
        if (!_inFlight.TryAdd(note.Id, 0))
        {
            return;
        }

        try
        {
            await SynchroniseNoteAsync(ownerUserId, note, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await SafeLogAsync(
                DiagnosticLevel.Error,
                DiagnosticEventNames.TripNoteSyncFailed,
                "Trip note synchronisation failed.",
                note,
                exception,
                cancellationToken);
            await MarkFailedAsync(ownerUserId, note, exception, cancellationToken);
        }
        finally
        {
            _inFlight.TryRemove(note.Id, out _);
        }
    }

    private async Task MarkFailedAsync(
        Guid ownerUserId,
        TripNoteModel note,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            var current = await _store.GetAsync(ownerUserId, note.TripId, note.Id, cancellationToken);
            if (current is null || current.SyncStatus == SyncStatus.Synchronised)
            {
                return;
            }

            var targetStatus = SynchronisationFailureClassifier.Classify(exception) == SynchronisationFailureKind.Permanent
                ? SyncStatus.FailedToSynchronise
                : SyncStatus.WaitingToSynchronise;
            await _store.SaveAsync(
                current with { SyncStatus = targetStatus },
                cancellationToken);
        }
        catch (Exception storeException) when (storeException is not OperationCanceledException)
        {
            await _logging.LogErrorAsync(
                "recording a failed trip note synchronisation",
                storeException,
                CancellationToken.None);
        }
    }

    private static bool NeedsAutomaticSynchronisation(TripNoteModel note)
    {
        return note.SyncStatus != SyncStatus.FailedToSynchronise;
    }

    private async Task SynchroniseNoteAsync(
        Guid ownerUserId,
        TripNoteModel note,
        CancellationToken cancellationToken)
    {
        await SafeLogAsync(
            DiagnosticLevel.Information,
            DiagnosticEventNames.TripNoteSyncStarted,
            "Trip note synchronisation started.",
            note,
            exception: null,
            cancellationToken);

        await _client.RecordNoteAsync(
            note.TripId,
            new RecordTripNoteDto(note.Id, note.Text, note.RecordedOn),
            cancellationToken);

        var current = await FindLocalAsync(ownerUserId, note, cancellationToken);
        if (current is null)
        {
            return;
        }

        await _store.SaveAsync(
            current with
            {
                SyncStatus = SyncStatus.Synchronised,
                SyncedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);
        await SafeLogAsync(
            DiagnosticLevel.Information,
            DiagnosticEventNames.TripNoteSyncCompleted,
            "Trip note synchronisation completed.",
            note,
            exception: null,
            cancellationToken);
    }

    private async Task<TripNoteModel?> FindLocalAsync(
        Guid ownerUserId,
        TripNoteModel note,
        CancellationToken cancellationToken)
    {
        var current = await _store.GetAsync(ownerUserId, note.TripId, note.Id, cancellationToken);
        if (current is null)
        {
            return null;
        }

        return string.Equals(current.Text, note.Text, StringComparison.Ordinal)
            && current.RecordedOn == note.RecordedOn
            ? current
            : null;
    }

    private async Task SafeLogAsync(
        DiagnosticLevel level,
        string eventName,
        string message,
        TripNoteModel note,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>
        {
            [DiagnosticMetadata.TripId] = note.TripId.ToString("D"),
            [DiagnosticMetadata.NoteId] = note.Id.ToString("D")
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
                    "trip note diagnostic",
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
