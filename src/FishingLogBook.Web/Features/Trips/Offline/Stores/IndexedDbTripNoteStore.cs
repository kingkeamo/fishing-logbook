using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Common.Offline;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.Trips.Offline.Stores;

public sealed class IndexedDbTripNoteStore : ITripNoteStore
{
    private const string ModulePath = "./js/offline-store.js";
    private const string StoreName = "trips";

    private readonly IJSRuntime _jsRuntime;
    private readonly IDiagnosticLogger _diagnostics;
    private readonly ILoggingService _logging;
    private readonly DiagnosticsClientConfig _config;
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;

    public IndexedDbTripNoteStore(
        IJSRuntime jsRuntime,
        IDiagnosticLogger diagnostics,
        ILoggingService logging,
        DiagnosticsClientConfig config)
    {
        _jsRuntime = jsRuntime;
        _diagnostics = diagnostics;
        _logging = logging;
        _config = config;
    }

    public async Task SaveAsync(TripNoteModel note, CancellationToken cancellationToken)
    {
        if (note.OwnerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip note requires an owner.");
        }

        if (note.Id == Guid.Empty || note.TripId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip note requires an identifier and a trip.");
        }

        if (string.IsNullOrWhiteSpace(note.Text))
        {
            throw new InvalidOperationException("A trip note requires text.");
        }

        var json = TripJson.SerializeNote(note);
        await OfflineOperation.ExecuteAsync(
            "write",
            StoreName,
            DiagnosticEventNames.OfflineDbWriteStarted,
            DiagnosticEventNames.OfflineDbWriteCompleted,
            DiagnosticEventNames.OfflineDbWriteFailed,
            DiagnosticEventNames.OfflineDbWriteTimedOut,
            _config.OperationTimeout,
            _diagnostics,
            async token =>
            {
                var module = await GetModuleAsync(token);
                return await module.InvokeAsync<bool>("putTripNote", token, json);
            },
            cancellationToken,
            _logging);
    }

    public async Task<bool> DeleteAsync(
        Guid ownerUserId,
        Guid tripId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        RequireOwner(ownerUserId);
        return await OfflineOperation.ExecuteAsync(
            "delete",
            StoreName,
            DiagnosticEventNames.OfflineDbWriteStarted,
            DiagnosticEventNames.OfflineDbWriteCompleted,
            DiagnosticEventNames.OfflineDbWriteFailed,
            DiagnosticEventNames.OfflineDbWriteTimedOut,
            _config.OperationTimeout,
            _diagnostics,
            async token =>
            {
                var module = await GetModuleAsync(token);
                return await module.InvokeAsync<bool>(
                    "deleteTripNote",
                    token,
                    ownerUserId.ToString("D"),
                    tripId.ToString("D"),
                    noteId.ToString("D"));
            },
            cancellationToken,
            _logging);
    }

    public async Task<IReadOnlyList<TripNoteModel>> GetForTripAsync(
        Guid ownerUserId,
        Guid tripId,
        CancellationToken cancellationToken)
    {
        RequireOwner(ownerUserId);
        var loaded = await ReadNotesAsync(
            "metadata-read",
            "getTripNotes",
            [ownerUserId.ToString("D"), tripId.ToString("D")],
            cancellationToken);
        return [.. loaded.Where(note => note.OwnerUserId == ownerUserId && note.TripId == tripId)];
    }

    public async Task<TripNoteModel?> GetAsync(
        Guid ownerUserId,
        Guid tripId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var notes = await GetForTripAsync(ownerUserId, tripId, cancellationToken);
        return notes.FirstOrDefault(note => note.Id == noteId);
    }

    private async Task<IReadOnlyList<TripNoteModel>> ReadNotesAsync(
        string operation,
        string jsFunction,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        return await OfflineOperation.ExecuteAsync(
            operation,
            StoreName,
            DiagnosticEventNames.OfflineDbReadStarted,
            DiagnosticEventNames.OfflineDbReadCompleted,
            DiagnosticEventNames.OfflineDbReadFailed,
            DiagnosticEventNames.OfflineDbReadTimedOut,
            _config.OperationTimeout,
            _diagnostics,
            async token =>
            {
                var module = await GetModuleAsync(token);
                var records = await module.InvokeAsync<StoredTripRecord[]>(jsFunction, token, arguments);
                return (IReadOnlyList<TripNoteModel>)(records ?? [])
                    .Select(record => TripJson.DeserializeNote(record.Json))
                    .ToArray();
            },
            cancellationToken,
            _logging);
    }

    public async Task<IReadOnlyList<TripNoteModel>> GetPendingAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        RequireOwner(ownerUserId);
        var loaded = await OfflineOperation.ExecuteAsync(
            "pending-read",
            StoreName,
            DiagnosticEventNames.OfflineDbReadStarted,
            DiagnosticEventNames.OfflineDbReadCompleted,
            DiagnosticEventNames.OfflineDbReadFailed,
            DiagnosticEventNames.OfflineDbReadTimedOut,
            _config.OperationTimeout,
            _diagnostics,
            async token =>
            {
                var module = await GetModuleAsync(token);
                var records = await module.InvokeAsync<StoredTripRecord[]>(
                    "getPendingTripNotes",
                    token,
                    ownerUserId.ToString("D"));
                return (IReadOnlyList<TripNoteModel>)(records ?? [])
                    .Select(record => TripJson.DeserializeNote(record.Json))
                    .ToArray();
            },
            cancellationToken,
            _logging);
        return [.. loaded.Where(note => note.OwnerUserId == ownerUserId)];
    }

    public async Task<IReadOnlyCollection<Guid>> GetTripsWithPendingNotesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        if (ownerUserId == Guid.Empty)
        {
            return [];
        }

        var loaded = await OfflineOperation.ExecuteAsync(
            "pending-read",
            StoreName,
            DiagnosticEventNames.OfflineDbReadStarted,
            DiagnosticEventNames.OfflineDbReadCompleted,
            DiagnosticEventNames.OfflineDbReadFailed,
            DiagnosticEventNames.OfflineDbReadTimedOut,
            _config.OperationTimeout,
            _diagnostics,
            async token =>
            {
                var module = await GetModuleAsync(token);
                return await module.InvokeAsync<Guid[]>(
                    "getTripsWithPendingNotes",
                    token,
                    ownerUserId.ToString("D"));
            },
            cancellationToken,
            _logging);
        return loaded ?? [];
    }

    private static void RequireOwner(Guid ownerUserId)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip note owner is required.");
        }
    }

    private async Task<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        if (_module is not null)
        {
            return _module;
        }

        await _moduleLock.WaitAsync(cancellationToken);
        try
        {
            _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
            return _module;
        }
        finally
        {
            _moduleLock.Release();
        }
    }
}
