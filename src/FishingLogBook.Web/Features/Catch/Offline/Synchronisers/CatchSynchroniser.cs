using System.Collections.Concurrent;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace FishingLogBook.Web.Features.Catch.Offline.Synchronisers;

public sealed class CatchSynchroniser : ICatchSynchroniser
{
    private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();
    private readonly ICatchStore _store;
    private readonly ICatchClient _client;
    private readonly INetworkService _networkService;
    private readonly ILocalCatchOwnerService _localCatchOwner;
    private readonly IDiagnosticLogger _diagnostics;
    private readonly ILoggingService _logging;

    public event EventHandler? StateChanged;

    public CatchSynchroniser(
        ICatchStore store,
        ICatchClient client,
        INetworkService networkService,
        ILocalCatchOwnerService localCatchOwner,
        IDiagnosticLogger diagnostics,
        ILoggingService logging)
    {
        _store = store;
        _client = client;
        _networkService = networkService;
        _localCatchOwner = localCatchOwner;
        _diagnostics = diagnostics;
        _logging = logging;
    }

    public async Task SynchronisePendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SynchronisePendingCoreAsync(cancellationToken);
        }
        finally
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task RetryAsync(Guid catchId, CancellationToken cancellationToken)
    {
        try
        {
            await RetryCoreAsync(catchId, cancellationToken);
        }
        finally
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task SynchronisePendingCoreAsync(CancellationToken cancellationToken)
    {
        var ownerUserId = await TryGetOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return;
        }

        var catches = await _store.GetAllAsync(ownerUserId.Value, cancellationToken);
        catches = await RecoverInterruptedAsync(catches, cancellationToken);
        var pending = catches.Where(NeedsSynchronisation).ToArray();
        if (!await _networkService.IsOnlineAsync(cancellationToken))
        {
            foreach (var catchRecord in pending)
            {
                await _store.UpdateSyncStateAsync(ToWaiting(catchRecord), cancellationToken);
            }

            return;
        }

        foreach (var catchRecord in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await SynchroniseGuardedAsync(ownerUserId.Value, catchRecord.Id, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await SafeLogAsync(
                    DiagnosticLevel.Error,
                    DiagnosticEventNames.CatchSyncFailed,
                    "Catch synchronisation failed.",
                    catchRecord.Id,
                    photographId: null,
                    exception,
                    cancellationToken);
            }
        }
    }

    private async Task RetryCoreAsync(Guid catchId, CancellationToken cancellationToken)
    {
        var ownerUserId = await TryGetOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return;
        }

        var catchRecord = await _store.GetAsync(ownerUserId.Value, catchId, cancellationToken);
        if (catchRecord is null || !NeedsSynchronisation(catchRecord))
        {
            return;
        }

        if (!await _networkService.IsOnlineAsync(cancellationToken))
        {
            await _store.UpdateSyncStateAsync(ToWaiting(catchRecord), cancellationToken);
            return;
        }

        await SynchroniseGuardedAsync(ownerUserId.Value, catchId, cancellationToken);
    }

    private async Task SynchroniseGuardedAsync(
        Guid ownerUserId,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        if (!_inFlight.TryAdd(catchId, 0))
        {
            return;
        }

        try
        {
            await SynchroniseCatchAsync(ownerUserId, catchId, cancellationToken);
        }
        finally
        {
            _inFlight.TryRemove(catchId, out _);
        }
    }

    private async Task SynchroniseCatchAsync(
        Guid ownerUserId,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        var catchRecord = await _store.GetAsync(ownerUserId, catchId, cancellationToken);
        if (catchRecord is null)
        {
            return;
        }

        await SafeLogAsync(
            DiagnosticLevel.Information,
            DiagnosticEventNames.CatchSyncStarted,
            "Catch synchronisation started.",
            catchId,
            photographId: null,
            exception: null,
            cancellationToken);
        catchRecord = catchRecord with { SyncStatus = SyncStatus.Synchronising };
        await _store.UpdateSyncStateAsync(catchRecord, cancellationToken);

        catchRecord = await SynchroniseMetadataAsync(catchRecord, cancellationToken);
        if (catchRecord.MetadataSyncStatus != SyncStatus.Synchronised)
        {
            if (catchRecord.MetadataSyncStatus == SyncStatus.FailedToSynchronise)
            {
                await SafeLogAsync(
                    DiagnosticLevel.Error,
                    DiagnosticEventNames.CatchSyncFailed,
                    "Catch synchronisation failed.",
                    catchId,
                    photographId: null,
                    exception: null,
                    cancellationToken);
            }

            return;
        }

        foreach (var photograph in catchRecord.Photographs.Where(NeedsPhotographSynchronisation).ToArray())
        {
            catchRecord = photograph.SyncStatus == SyncStatus.PendingDeletion
                ? await DeletePhotographAsync(catchRecord, photograph.Id, cancellationToken)
                : await SynchronisePhotographAsync(catchRecord, photograph.Id, cancellationToken);
        }

        catchRecord = catchRecord with { SyncStatus = DeriveOverallStatus(catchRecord) };
        await _store.UpdateSyncStateAsync(catchRecord, cancellationToken);
        await SafeLogAsync(
            catchRecord.SyncStatus == SyncStatus.Synchronised
                ? DiagnosticLevel.Information
                : DiagnosticLevel.Error,
            catchRecord.SyncStatus == SyncStatus.Synchronised
                ? DiagnosticEventNames.CatchSyncCompleted
                : DiagnosticEventNames.CatchSyncFailed,
            catchRecord.SyncStatus == SyncStatus.Synchronised
                ? "Catch synchronisation completed."
                : "Catch synchronisation failed.",
            catchId,
            photographId: null,
            exception: null,
            cancellationToken);
    }

    private async Task<CatchModel> SynchroniseMetadataAsync(
        CatchModel catchRecord,
        CancellationToken cancellationToken)
    {
        if (catchRecord.MetadataSyncStatus == SyncStatus.Synchronised)
        {
            return catchRecord;
        }

        catchRecord = catchRecord with { MetadataSyncStatus = SyncStatus.Synchronising };
        await _store.UpdateSyncStateAsync(catchRecord, cancellationToken);
        try
        {
            var sent = ToDto(catchRecord);
            await _client.UpsertAsync(sent, cancellationToken);
            var current = await _store.GetAsync(
                catchRecord.UserId,
                catchRecord.Id,
                cancellationToken);
            if (current is null)
            {
                return catchRecord;
            }

            if (!HasSameMetadata(current, sent))
            {
                current = current with
                {
                    SyncStatus = SyncStatus.WaitingToSynchronise,
                    MetadataSyncStatus = SyncStatus.WaitingToSynchronise
                };
                await _store.UpdateSyncStateAsync(current, cancellationToken);
                return current;
            }

            catchRecord = current with { MetadataSyncStatus = SyncStatus.Synchronised };
            await _store.UpdateSyncStateAsync(catchRecord, cancellationToken);
            await SafeLogAsync(
                DiagnosticLevel.Information,
                DiagnosticEventNames.CatchMetadataSyncSucceeded,
                "Catch metadata synchronisation succeeded.",
                catchRecord.Id,
                photographId: null,
                exception: null,
                cancellationToken);
            return catchRecord;
        }
        catch (Exception exception) when (IsSynchronisationFailure(exception, cancellationToken))
        {
            catchRecord = catchRecord with
            {
                SyncStatus = SyncStatus.FailedToSynchronise,
                MetadataSyncStatus = SyncStatus.FailedToSynchronise
            };
            await _store.UpdateSyncStateAsync(catchRecord, cancellationToken);
            await SafeLogAsync(
                DiagnosticLevel.Error,
                DiagnosticEventNames.CatchMetadataSyncFailed,
                "Catch metadata synchronisation failed.",
                catchRecord.Id,
                photographId: null,
                exception,
                cancellationToken);
            if (IsAuthenticationFailure(exception))
            {
                await SafeLogAsync(
                    DiagnosticLevel.Warning,
                    DiagnosticEventNames.AuthenticationUnavailable,
                    "Authentication is unavailable for catch synchronisation.",
                    catchRecord.Id,
                    photographId: null,
                    exception,
                    cancellationToken);
            }

            return catchRecord;
        }
    }

    private async Task<CatchModel> SynchronisePhotographAsync(
        CatchModel catchRecord,
        Guid photographId,
        CancellationToken cancellationToken)
    {
        catchRecord = WithPhotographStatus(
            catchRecord,
            photographId,
            SyncStatus.Synchronising,
            objectKey: null);
        await _store.UpdateSyncStateAsync(catchRecord, cancellationToken);
        await SafeLogAsync(
            DiagnosticLevel.Information,
            DiagnosticEventNames.PhotographUploadStarted,
            "Catch photograph upload started.",
            catchRecord.Id,
            photographId,
            exception: null,
            cancellationToken);

        var photograph = catchRecord.Photographs.Single(item => item.Id == photographId);
        try
        {
            if (photograph.Bytes is not { Length: > 0 })
            {
                throw new InvalidOperationException("Catch photograph bytes are unavailable.");
            }

            var upload = await _client.CreatePhotographUploadAsync(
                catchRecord.Id,
                new PhotographUploadRequestDto(photograph.Id, photograph.ContentType),
                cancellationToken);
            await _client.UploadPhotographAsync(
                upload.UploadUrl,
                photograph.Bytes,
                photograph.ContentType,
                cancellationToken);
            await _client.RecordPhotographAsync(
                catchRecord.Id,
                new RecordPhotographDto(
                    photograph.Id,
                    upload.ObjectKey,
                    photograph.ContentType),
                cancellationToken);
            catchRecord = WithPhotographStatus(
                catchRecord,
                photographId,
                SyncStatus.Synchronised,
                upload.ObjectKey);
            await _store.UpdateSyncStateAsync(catchRecord, cancellationToken);
            await SafeLogAsync(
                DiagnosticLevel.Information,
                DiagnosticEventNames.PhotographUploadSucceeded,
                "Catch photograph upload succeeded.",
                catchRecord.Id,
                photographId,
                exception: null,
                cancellationToken);
            return catchRecord;
        }
        catch (Exception exception) when (IsSynchronisationFailure(exception, cancellationToken))
        {
            catchRecord = WithPhotographStatus(
                catchRecord,
                photographId,
                SyncStatus.FailedToSynchronise,
                objectKey: null);
            await _store.UpdateSyncStateAsync(catchRecord, cancellationToken);
            await SafeLogAsync(
                DiagnosticLevel.Error,
                DiagnosticEventNames.PhotographUploadFailed,
                "Catch photograph upload failed.",
                catchRecord.Id,
                photographId,
                exception,
                cancellationToken);
            if (IsAuthenticationFailure(exception))
            {
                await SafeLogAsync(
                    DiagnosticLevel.Warning,
                    DiagnosticEventNames.AuthenticationUnavailable,
                    "Authentication is unavailable for catch synchronisation.",
                    catchRecord.Id,
                    photographId,
                    exception,
                    cancellationToken);
            }

            return catchRecord;
        }
    }

    private async Task<CatchModel> DeletePhotographAsync(
        CatchModel catchRecord,
        Guid photographId,
        CancellationToken cancellationToken)
    {
        await SafeLogAsync(
            DiagnosticLevel.Information,
            DiagnosticEventNames.PhotographDeleteStarted,
            "Catch photograph delete started.",
            catchRecord.Id,
            photographId,
            exception: null,
            cancellationToken);

        try
        {
            await _client.DeletePhotographAsync(catchRecord.Id, photographId, cancellationToken);
            catchRecord = WithoutPhotograph(catchRecord, photographId);
            await _store.SaveAsync(catchRecord, cancellationToken);
            await SafeLogAsync(
                DiagnosticLevel.Information,
                DiagnosticEventNames.PhotographDeleteSucceeded,
                "Catch photograph delete succeeded.",
                catchRecord.Id,
                photographId,
                exception: null,
                cancellationToken);
            return catchRecord;
        }
        catch (Exception exception) when (IsSynchronisationFailure(exception, cancellationToken))
        {
            await SafeLogAsync(
                DiagnosticLevel.Error,
                DiagnosticEventNames.PhotographDeleteFailed,
                "Catch photograph delete failed.",
                catchRecord.Id,
                photographId,
                exception,
                cancellationToken);
            if (IsAuthenticationFailure(exception))
            {
                await SafeLogAsync(
                    DiagnosticLevel.Warning,
                    DiagnosticEventNames.AuthenticationUnavailable,
                    "Authentication is unavailable for catch synchronisation.",
                    catchRecord.Id,
                    photographId,
                    exception,
                    cancellationToken);
            }

            return catchRecord;
        }
    }

    private static CatchModel WithoutPhotograph(CatchModel catchRecord, Guid photographId)
    {
        return catchRecord with
        {
            Photographs = catchRecord.Photographs
                .Where(photograph => photograph.Id != photographId)
                .ToArray()
        };
    }

    private async Task<IReadOnlyList<CatchModel>> RecoverInterruptedAsync(
        IReadOnlyList<CatchModel> catches,
        CancellationToken cancellationToken)
    {
        var recovered = new List<CatchModel>(catches.Count);
        foreach (var catchRecord in catches)
        {
            var retryable = RecoverInterrupted(catchRecord);
            recovered.Add(retryable);
            if (retryable != catchRecord)
            {
                await _store.UpdateSyncStateAsync(retryable, cancellationToken);
            }
        }

        return recovered;
    }

    private async Task<Guid?> TryGetOwnerUserIdAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _localCatchOwner.GetUserIdAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is AccessTokenNotAvailableException
                                          or InvalidOperationException
                                          or HttpRequestException)
        {
            await SafeLogAsync(
                DiagnosticLevel.Warning,
                DiagnosticEventNames.AuthenticationUnavailable,
                "Authentication is unavailable for catch synchronisation.",
                catchId: null,
                photographId: null,
                exception,
                cancellationToken);
            return null;
        }
    }

    private async Task SafeLogAsync(
        DiagnosticLevel level,
        string eventName,
        string message,
        Guid? catchId,
        Guid? photographId,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>();
        if (catchId is not null)
        {
            metadata[DiagnosticMetadata.CatchId] = catchId.Value.ToString("D");
        }

        if (photographId is not null)
        {
            metadata[DiagnosticMetadata.PhotographId] = photographId.Value.ToString("D");
        }

        if (exception is not null)
        {
            metadata[DiagnosticMetadata.ErrorType] = exception.GetType().Name;
        }

        try
        {
            await _diagnostics.LogAsync(
                level,
                eventName,
                message,
                metadata,
                cancellationToken: cancellationToken);
        }
        catch (Exception loggingException)
        {
            try
            {
                await _logging.LogErrorAsync(
                    "catch synchronisation diagnostic",
                    loggingException,
                    CancellationToken.None);
            }
            catch (Exception)
            {
                // Diagnostics never control synchronisation state.
            }
        }
    }

    private static CatchModel RecoverInterrupted(CatchModel catchRecord)
    {
        var photographs = catchRecord.Photographs
            .Select(photograph => photograph.SyncStatus == SyncStatus.Synchronising
                ? photograph with { SyncStatus = SyncStatus.WaitingToSynchronise }
                : photograph)
            .ToArray();
        return catchRecord with
        {
            SyncStatus = catchRecord.SyncStatus == SyncStatus.Synchronising
                ? SyncStatus.WaitingToSynchronise
                : catchRecord.SyncStatus,
            MetadataSyncStatus = catchRecord.MetadataSyncStatus == SyncStatus.Synchronising
                ? SyncStatus.WaitingToSynchronise
                : catchRecord.MetadataSyncStatus,
            Photographs = photographs
        };
    }

    private static CatchModel ToWaiting(CatchModel catchRecord)
    {
        var photographs = catchRecord.Photographs
            .Select(photograph => photograph.SyncStatus == SyncStatus.Synchronised
                ? photograph
                : photograph with { SyncStatus = SyncStatus.WaitingToSynchronise })
            .ToArray();
        return catchRecord with
        {
            SyncStatus = SyncStatus.WaitingToSynchronise,
            MetadataSyncStatus = catchRecord.MetadataSyncStatus == SyncStatus.Synchronised
                ? SyncStatus.Synchronised
                : SyncStatus.WaitingToSynchronise,
            Photographs = photographs
        };
    }

    private static CatchModel WithPhotographStatus(
        CatchModel catchRecord,
        Guid photographId,
        SyncStatus status,
        string? objectKey)
    {
        return catchRecord with
        {
            Photographs = catchRecord.Photographs
                .Select(photograph => photograph.Id == photographId
                    ? photograph with
                    {
                        SyncStatus = status,
                        ObjectKey = objectKey ?? photograph.ObjectKey
                    }
                    : photograph)
                .ToArray()
        };
    }

    private static SyncStatus DeriveOverallStatus(CatchModel catchRecord)
    {
        if (catchRecord.MetadataSyncStatus == SyncStatus.Synchronising
            || catchRecord.Photographs.Any(
                photograph => photograph.SyncStatus == SyncStatus.Synchronising))
        {
            return SyncStatus.Synchronising;
        }

        if (catchRecord.MetadataSyncStatus == SyncStatus.FailedToSynchronise
            || catchRecord.Photographs.Any(
                photograph => photograph.SyncStatus == SyncStatus.FailedToSynchronise))
        {
            return SyncStatus.FailedToSynchronise;
        }

        if (catchRecord.MetadataSyncStatus == SyncStatus.Synchronised
            && catchRecord.Photographs.All(
                photograph => photograph.SyncStatus == SyncStatus.Synchronised))
        {
            return SyncStatus.Synchronised;
        }

        return SyncStatus.WaitingToSynchronise;
    }

    private static CatchDto ToDto(CatchModel catchRecord)
    {
        return new CatchDto(
            catchRecord.Id,
            catchRecord.CaughtOn,
            catchRecord.Photographs
                .Select(photograph => new CatchPhotographDto(
                    photograph.Id,
                    photograph.CatchId,
                    photograph.ContentType))
                .ToArray(),
            catchRecord.Location is null
                ? null
                : new CatchLocationDto(
                    catchRecord.Location.Latitude,
                    catchRecord.Location.Longitude,
                    catchRecord.Location.AccuracyMetres,
                    catchRecord.Location.CapturedOn,
                    catchRecord.Location.Source,
                    catchRecord.Location.Visibility,
                    catchRecord.Location.ConsentVersion))
        {
            UserId = catchRecord.UserId,
            AnglerUserId = catchRecord.AnglerUserId,
            RecordedByUserId = catchRecord.RecordedByUserId,
            SpeciesName = catchRecord.SpeciesName,
            Weight = catchRecord.Weight,
            Length = catchRecord.Length,
            Method = catchRecord.Method,
            BaitOrLure = catchRecord.BaitOrLure,
            Notes = catchRecord.Notes
        };
    }

    private static bool HasSameMetadata(CatchModel catchRecord, CatchDto sent)
    {
        return catchRecord.Id == sent.Id
            && catchRecord.CaughtOn == sent.CaughtOn
            && catchRecord.UserId == sent.UserId
            && catchRecord.AnglerUserId == sent.AnglerUserId
            && catchRecord.RecordedByUserId == sent.RecordedByUserId
            && HaveSameDetails(catchRecord, sent)
            && HaveSamePhotographs(catchRecord, sent)
            && HaveSameLocation(catchRecord, sent);
    }

    private static bool HaveSameDetails(CatchModel catchRecord, CatchDto sent)
    {
        return string.Equals(catchRecord.SpeciesName, sent.SpeciesName, StringComparison.Ordinal)
            && catchRecord.Weight == sent.Weight
            && catchRecord.Length == sent.Length
            && string.Equals(catchRecord.Method, sent.Method, StringComparison.Ordinal)
            && string.Equals(catchRecord.BaitOrLure, sent.BaitOrLure, StringComparison.Ordinal)
            && string.Equals(catchRecord.Notes, sent.Notes, StringComparison.Ordinal);
    }

    private static bool HaveSamePhotographs(CatchModel catchRecord, CatchDto sent)
    {
        if (catchRecord.Photographs.Count != sent.Photographs.Count)
        {
            return false;
        }

        return catchRecord.Photographs.All(photograph =>
            sent.Photographs.Any(dto =>
                dto.Id == photograph.Id
                && dto.CatchId == photograph.CatchId
                && string.Equals(
                    dto.ContentType,
                    photograph.ContentType,
                    StringComparison.OrdinalIgnoreCase)));
    }

    private static bool HaveSameLocation(CatchModel catchRecord, CatchDto sent)
    {
        if (catchRecord.Location is null || sent.Location is null)
        {
            return catchRecord.Location is null && sent.Location is null;
        }

        return catchRecord.Location.Latitude == sent.Location.Latitude
            && catchRecord.Location.Longitude == sent.Location.Longitude
            && catchRecord.Location.AccuracyMetres == sent.Location.AccuracyMetres
            && catchRecord.Location.CapturedOn == sent.Location.CapturedOn
            && string.Equals(
                catchRecord.Location.Source,
                sent.Location.Source,
                StringComparison.Ordinal)
            && string.Equals(
                catchRecord.Location.Visibility,
                sent.Location.Visibility,
                StringComparison.Ordinal)
            && string.Equals(
                catchRecord.Location.ConsentVersion,
                sent.Location.ConsentVersion,
                StringComparison.Ordinal);
    }

    private static bool NeedsSynchronisation(CatchModel catchRecord)
    {
        return catchRecord.SyncStatus != SyncStatus.Synchronised;
    }

    private static bool NeedsPhotographSynchronisation(CatchPhotographModel photograph)
    {
        return photograph.SyncStatus != SyncStatus.Synchronised;
    }

    private static bool IsSynchronisationFailure(
        Exception exception,
        CancellationToken cancellationToken)
    {
        return exception is not OperationCanceledException
            || !cancellationToken.IsCancellationRequested;
    }

    private static bool IsAuthenticationFailure(Exception exception)
    {
        return exception is AccessTokenNotAvailableException
            || exception is HttpRequestException
            {
                StatusCode: System.Net.HttpStatusCode.Unauthorized
            };
    }
}
