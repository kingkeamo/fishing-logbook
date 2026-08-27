using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Common.Offline;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.Trips.Offline.Stores;

public sealed class IndexedDbTripPhotographStore : ITripPhotographStore
{
    private const string ModulePath = "./js/offline-store.js";
    private const string StoreName = "tripPhotographs";

    private readonly IJSRuntime _jsRuntime;
    private readonly IDiagnosticLogger _diagnostics;
    private readonly ILoggingService _logging;
    private readonly DiagnosticsClientConfig _config;
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;

    public IndexedDbTripPhotographStore(
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

    public async Task SaveAsync(TripPhotographModel photograph, CancellationToken cancellationToken)
    {
        if (photograph.OwnerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip photograph requires an owner.");
        }

        if (photograph.Id == Guid.Empty || photograph.TripId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip photograph requires an identifier and a trip.");
        }

        if (photograph.Bytes is not { Length: > 0 })
        {
            throw new InvalidOperationException("A trip photograph requires prepared bytes.");
        }

        var json = TripJson.SerializePhotograph(photograph);
        var bytes = photograph.Bytes;
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
                return await module.InvokeAsync<bool>("putTripPhotograph", token, json, bytes);
            },
            cancellationToken,
            _logging);
    }

    public async Task<byte[]?> GetBytesAsync(
        Guid ownerUserId,
        Guid tripId,
        Guid photographId,
        CancellationToken cancellationToken)
    {
        RequireOwner(ownerUserId);
        return await OfflineOperation.ExecuteAsync(
            "photo-read",
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
                return await module.InvokeAsync<byte[]?>(
                    "getTripPhotographBytes",
                    token,
                    ownerUserId.ToString("D"),
                    tripId.ToString("D"),
                    photographId.ToString("D"));
            },
            cancellationToken,
            _logging);
    }

    public async Task<bool> DeleteAsync(
        Guid ownerUserId,
        Guid tripId,
        Guid photographId,
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
                    "deleteTripPhotograph",
                    token,
                    ownerUserId.ToString("D"),
                    tripId.ToString("D"),
                    photographId.ToString("D"));
            },
            cancellationToken,
            _logging);
    }

    public async Task<IReadOnlyList<TripPhotographModel>> GetPendingAsync(
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
                    "getPendingTripPhotographs",
                    token,
                    ownerUserId.ToString("D"));
                return (IReadOnlyList<TripPhotographModel>)(records ?? [])
                    .Select(record => TripJson.DeserializePhotograph(record.Json))
                    .ToArray();
            },
            cancellationToken,
            _logging);
        return [.. loaded.Where(photograph => photograph.OwnerUserId == ownerUserId)];
    }

    public async Task<IReadOnlyCollection<Guid>> GetTripsWithPendingPhotographsAsync(
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
                    "getTripsWithPendingPhotographs",
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
            throw new InvalidOperationException("A trip photograph owner is required.");
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
