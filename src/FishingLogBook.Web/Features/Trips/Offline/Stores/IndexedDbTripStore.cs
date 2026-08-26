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
        if (trip.OwnerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip requires an owner.");
        }

        if (trip.Id == Guid.Empty)
        {
            throw new InvalidOperationException("A trip requires an identifier.");
        }

        var json = TripJson.Serialize(trip);
        var outcome = await OfflineOperation.ExecuteAsync(
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
                return await module.InvokeAsync<string>("putTrip", token, json);
            },
            cancellationToken,
            _logging);

        if (string.Equals(outcome, ActiveConflictOutcome, StringComparison.Ordinal))
        {
            throw new TripAlreadyActiveException();
        }
    }

    public async Task<IReadOnlyList<TripModel>> GetAllAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip owner is required.");
        }

        var loaded = await OfflineOperation.ExecuteAsync(
            "metadata-read",
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
                    "getTrips",
                    token,
                    ownerUserId.ToString("D"));
                return (IReadOnlyList<TripModel>)(records ?? [])
                    .Select(record => TripJson.Deserialize(record.Json))
                    .ToArray();
            },
            cancellationToken,
            _logging);
        return LocalTripVisibility.ForOwner(loaded, ownerUserId);
    }

    public async Task<TripModel?> GetAsync(
        Guid ownerUserId,
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return await ReadSingleAsync(
            ownerUserId,
            "single-read",
            "getTrip",
            [ownerUserId.ToString("D"), tripId.ToString("D")],
            cancellationToken);
    }

    public async Task<TripModel?> GetActiveAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        return await ReadSingleAsync(
            ownerUserId,
            "active-read",
            "getActiveTrip",
            [ownerUserId.ToString("D")],
            cancellationToken);
    }

    private async Task<TripModel?> ReadSingleAsync(
        Guid ownerUserId,
        string operation,
        string jsFunction,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip owner is required.");
        }

        var loaded = await OfflineOperation.ExecuteAsync(
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
        return loaded is not null && loaded.OwnerUserId == ownerUserId ? loaded : null;
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
