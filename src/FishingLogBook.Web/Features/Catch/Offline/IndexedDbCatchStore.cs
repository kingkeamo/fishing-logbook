using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.Catch.Offline;

public sealed class IndexedDbCatchStore : ICatchStore
{
    private const string ModulePath = "./js/offline-store.js";
    private const string StoreName = "catches";

    private readonly IJSRuntime _jsRuntime;
    private readonly IDiagnosticLogger _diagnostics;
    private readonly ILoggingService _logging;
    private readonly DiagnosticsClientConfig _config;
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;

    public IndexedDbCatchStore(
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

    public async Task SaveAsync(CatchModel catchRecord, CancellationToken cancellationToken)
    {
        if (catchRecord.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("A catch requires an owner.");
        }

        if (catchRecord.Photographs.Count == 0
            || catchRecord.Photographs.Any(photograph => photograph.Bytes is not { Length: > 0 }))
        {
            throw new InvalidOperationException("A catch requires at least one photograph.");
        }

        var json = CatchJson.SerializeMetadata(catchRecord);
        var photographs = catchRecord.Photographs
            .Select(photograph => new StoredCatchPhotographWrite
            {
                Id = photograph.Id.ToString("D"),
                CatchId = photograph.CatchId.ToString("D"),
                ContentType = photograph.ContentType,
                Bytes = photograph.Bytes!
            })
            .ToArray();

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
                await module.InvokeVoidAsync("putCatchWithPhotographs", token, json, photographs);
            },
            cancellationToken,
            _logging);
    }

    public async Task<IReadOnlyList<CatchModel>> GetAllAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A catch owner is required.");
        }

        var loaded = await OfflineOperation.ExecuteAsync(
            "read",
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
                var records = await module.InvokeAsync<StoredCatchRecord[]>(
                    "getAllCatchesWithPhotographs",
                    token,
                    ownerUserId.ToString("D"));
                return (IReadOnlyList<CatchModel>)(records ?? []).Select(ToModel).ToArray();
            },
            cancellationToken,
            _logging);
        return LocalCatchVisibility.ForOwner(loaded, ownerUserId);
    }

    public async Task<CatchModel?> GetAsync(
        Guid ownerUserId,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        var catches = await GetAllAsync(ownerUserId, cancellationToken);
        return catches.SingleOrDefault(catchRecord => catchRecord.Id == catchId);
    }

    public async Task UpdateSyncStateAsync(
        CatchModel catchRecord,
        CancellationToken cancellationToken)
    {
        if (catchRecord.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("A catch requires an owner.");
        }

        var json = CatchJson.SerializeMetadata(catchRecord);
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
                await module.InvokeVoidAsync("updateCatchMetadata", token, json);
            },
            cancellationToken,
            _logging);
    }

    private static CatchModel ToModel(StoredCatchRecord record)
    {
        var photographs = record.Photographs
            .Select(photograph => new CatchPhotographModel(
                Guid.Parse(photograph.Id),
                Guid.Parse(photograph.CatchId),
                photograph.ContentType,
                Convert.FromBase64String(photograph.BytesBase64)))
            .ToArray();
        return CatchJson.Deserialize(record.Json, photographs);
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
