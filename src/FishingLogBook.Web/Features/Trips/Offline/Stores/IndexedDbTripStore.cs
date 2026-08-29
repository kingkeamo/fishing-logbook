using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Common.Offline;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.Trips.Offline.Stores;

public sealed class IndexedDbTripStore : ITripStore
{
    private const string ModulePath = "./js/offline-store.js";
    private const string StoreName = "trips";
    private const string ActiveConflictOutcome = "activeConflict";

    private readonly IJSRuntime _jsRuntime;
    private readonly IDiagnosticLogger _diagnostics;
    private readonly ILoggingService _logging;
    private readonly DiagnosticsClientConfig _config;
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;

    public IndexedDbTripStore(
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

    public async Task SaveAsync(TripModel trip, CancellationToken cancellationToken)
    {
        RequireTrip(trip);
        var json = TripJson.Serialize(trip);
        var outcome = await WriteAsync(
            async (module, token) => await module.InvokeAsync<string>("putTrip", token, json),
            cancellationToken);

        if (string.Equals(outcome, ActiveConflictOutcome, StringComparison.Ordinal))
        {
            throw new TripAlreadyActiveException();
        }
    }

    public async Task HydrateAsync(TripModel trip, Guid viewerUserId, CancellationToken cancellationToken)
    {
        RequireTrip(trip);
        if (!trip.CanContribute(viewerUserId))
        {
            throw new InvalidOperationException("A shared trip can only be cached for a contributor.");
        }

        var json = TripJson.Serialize(trip);
        await WriteAsync(
            async (module, token) => await module.InvokeAsync<string>(
                "hydrateTrip",
                token,
                json,
                viewerUserId.ToString("D")),
            cancellationToken);
    }

    public async Task<IReadOnlyList<TripModel>> GetAllAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        RequireViewer(viewerUserId);
        var loaded = await ReadManyAsync(
            "metadata-read",
            "getTrips",
            [viewerUserId.ToString("D")],
            cancellationToken);
        return LocalTripAccess.ForViewer(loaded, viewerUserId);
    }

    public async Task<TripModel?> GetAsync(
        Guid viewerUserId,
        Guid tripId,
        CancellationToken cancellationToken)
    {
        RequireViewer(viewerUserId);
        var loaded = await ReadSingleAsync(
            "single-read",
            "getTrip",
            [viewerUserId.ToString("D"), tripId.ToString("D")],
            cancellationToken);
        return loaded is not null && loaded.CanContribute(viewerUserId) ? loaded : null;
    }

    public async Task<TripModel?> GetActiveAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        RequireViewer(ownerUserId);
        var loaded = await ReadSingleAsync(
            "active-read",
            "getActiveTrip",
            [ownerUserId.ToString("D")],
            cancellationToken);
        return loaded is not null && loaded.IsOwnedBy(ownerUserId) ? loaded : null;
    }

    public async Task<IReadOnlyList<TripModel>> GetPendingAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        RequireViewer(ownerUserId);
        var loaded = await ReadManyAsync(
            "pending-read",
            "getPendingTrips",
            [ownerUserId.ToString("D")],
            cancellationToken);
        return LocalTripAccess.OwnedBy(loaded, ownerUserId);
    }

    public async Task<int> CleanupSyncedAsync(
        Guid viewerUserId,
        DateTimeOffset olderThan,
        IReadOnlyCollection<Guid> retainedTripIds,
        CancellationToken cancellationToken)
    {
        RequireViewer(viewerUserId);
        return await OfflineOperation.ExecuteAsync(
            "cleanup",
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
                return await module.InvokeAsync<int>(
                    "cleanupSyncedTrips",
                    token,
                    viewerUserId.ToString("D"),
                    olderThan.ToUniversalTime().ToString("O"),
                    retainedTripIds.Select(tripId => tripId.ToString("D")).ToArray());
            },
            cancellationToken,
            _logging);
    }

    private async Task<string> WriteAsync(
        Func<IJSObjectReference, CancellationToken, Task<string>> invoke,
        CancellationToken cancellationToken)
    {
        return await OfflineOperation.ExecuteAsync(
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
                return await invoke(module, token);
            },
            cancellationToken,
            _logging);
    }

    private async Task<IReadOnlyList<TripModel>> ReadManyAsync(
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
                return (IReadOnlyList<TripModel>)(records ?? [])
                    .Select(record => TripJson.Deserialize(record.Json))
                    .ToArray();
            },
            cancellationToken,
            _logging);
    }

    private async Task<TripModel?> ReadSingleAsync(
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
                var record = await module.InvokeAsync<StoredTripRecord?>(jsFunction, token, arguments);
                return record is null ? null : TripJson.Deserialize(record.Json);
            },
            cancellationToken,
            _logging);
    }

    private static void RequireTrip(TripModel trip)
    {
        if (trip.OwnerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip requires an owner.");
        }

        if (trip.Id == Guid.Empty)
        {
            throw new InvalidOperationException("A trip requires an identifier.");
        }
    }

    private static void RequireViewer(Guid viewerUserId)
    {
        if (viewerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip angler is required.");
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
