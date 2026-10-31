using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Catch.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Photographs.Clients;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Catch.Services;

public sealed class CatchWorkingCopyService(
    ICatchStore catchStore,
    ICatchRemoteClient remoteClient,
    IPhotographDownloadClient downloadClient,
    IPhotographFileStore fileStore,
    ICatchWorkingCopyStore workingCopyStore,
    IConnectivityService connectivity,
    IAccountSessionCoordinator sessionCoordinator,
    IActiveCatchEditRegistry activeEdits,
    ILocalCatchCleanupStore cleanupStore,
    TimeProvider timeProvider,
    ILogger<CatchWorkingCopyService> logger) : ICatchWorkingCopyService
{
    public async Task<WorkingCopyOutcomeEnum> EnsureAsync(Guid catchId, CancellationToken cancellationToken)
    {
        using var edit = await activeEdits.AcquireAsync(catchId, cancellationToken);
        var lease = sessionCoordinator.Capture();
        if (lease is null)
        {
            return WorkingCopyOutcomeEnum.SessionExpired;
        }

        if (await catchStore.GetAsync(catchId, cancellationToken) is not null)
        {
            return sessionCoordinator.IsCurrent(lease) ? WorkingCopyOutcomeEnum.Ready : WorkingCopyOutcomeEnum.SessionExpired;
        }

        if (!connectivity.IsOnline)
        {
            return WorkingCopyOutcomeEnum.NeedsConnection;
        }

        var remote = await remoteClient.GetAsync(catchId, cancellationToken);
        if (!remote.IsSuccess)
        {
            return remote.Outcome switch
            {
                ApiOutcomeEnum.NotFound => WorkingCopyOutcomeEnum.NotFound,
                ApiOutcomeEnum.SessionExpired => WorkingCopyOutcomeEnum.SessionExpired,
                _ => WorkingCopyOutcomeEnum.NeedsConnection
            };
        }

        var view = remote.Value!;
        if (view.RecordedByUserId != lease.UserId || view.CaughtByUserId != lease.UserId || view.TripId is not null)
        {
            return WorkingCopyOutcomeEnum.NotEditable;
        }

        return await HydrateAsync(view, lease, cancellationToken);
    }

    private async Task<WorkingCopyOutcomeEnum> HydrateAsync(CatchViewDto view, AccountSessionLeaseModel lease, CancellationToken cancellationToken)
    {
        if (view.Photographs.Count == 0)
        {
            return WorkingCopyOutcomeEnum.PhotosUnavailable;
        }

        try
        {
            var photographs = await DownloadAsync(lease.UserId, view, cancellationToken);
            return photographs is null
                ? WorkingCopyOutcomeEnum.PhotosUnavailable
                : await CreateAsync(view, photographs, lease, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to hydrate catch {CatchId}.", view.Id);
            return WorkingCopyOutcomeEnum.PhotosUnavailable;
        }
    }

    private async Task<WorkingCopyOutcomeEnum> CreateAsync(
        CatchViewDto view,
        IReadOnlyList<LocalCatchPhotographModel> photographs,
        AccountSessionLeaseModel lease,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        try
        {
            if (!sessionCoordinator.IsCurrent(lease))
            {
                await RollbackAsync(lease.UserId, photographs);
                return WorkingCopyOutcomeEnum.SessionExpired;
            }

            var record = ToRecord(lease.UserId, view, photographs, now);
            var created = await workingCopyStore.TryCreateAsync(record, now, cancellationToken);
            if (!created && await catchStore.GetAsync(view.Id, cancellationToken) is null)
            {
                await RollbackAsync(lease.UserId, photographs);
                return WorkingCopyOutcomeEnum.PhotosUnavailable;
            }

            return WorkingCopyOutcomeEnum.Ready;
        }
        catch (OperationCanceledException)
        {
            await RollbackAsync(lease.UserId, photographs);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create the working copy of catch {CatchId}.", view.Id);
            await RollbackAsync(lease.UserId, photographs);
            return WorkingCopyOutcomeEnum.PhotosUnavailable;
        }
    }

    private async Task<IReadOnlyList<LocalCatchPhotographModel>?> DownloadAsync(
        Guid userId,
        CatchViewDto view,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var saved = new List<LocalCatchPhotographModel>();
        foreach (var photograph in view.Photographs)
        {
            var download = string.IsNullOrWhiteSpace(photograph.Url)
                ? null
                : await downloadClient.DownloadAsync(photograph.Url, cancellationToken);
            var relativePath = download?.Outcome == PhotographDownloadOutcomeEnum.Downloaded
                ? await SaveTrackedAsync(userId, photograph.ContentType, download.Bytes!, cancellationToken)
                : null;
            if (relativePath is null)
            {
                await RollbackAsync(userId, saved);
                return null;
            }

            saved.Add(new LocalCatchPhotographModel
            {
                Id = photograph.Id,
                CatchId = view.Id,
                UserId = userId,
                SortOrder = saved.Count,
                LocalFilePath = relativePath,
                ContentType = photograph.ContentType,
                CreatedOn = now
            });
        }

        return saved;
    }

    private async Task<string?> SaveTrackedAsync(Guid userId, string contentType, byte[] bytes, CancellationToken cancellationToken)
    {
        var storageId = Guid.NewGuid();
        var path = fileStore.GetRelativePath(userId, storageId, contentType);
        if (path is null)
        {
            return null;
        }

        await cleanupStore.QueueFileDeletionAsync(userId, path, timeProvider.GetUtcNow(), cancellationToken);
        return await fileStore.SaveAsync(userId, storageId, contentType, bytes, cancellationToken);
    }

    private async Task RollbackAsync(Guid userId, IReadOnlyList<LocalCatchPhotographModel> photographs)
    {
        var paths = photographs.Select(photograph => photograph.LocalFilePath).OfType<string>().ToHashSet();
        foreach (var (id, path) in await cleanupStore.ListPendingFileDeletionsAsync(userId, CancellationToken.None))
        {
            if (paths.Contains(path))
            {
                await cleanupStore.TryDeleteFileAsync(userId, id, path, () => fileStore.TryDelete(userId, path), CancellationToken.None);
            }
        }
    }

    private static LocalCatchRecordModel ToRecord(
        Guid userId,
        CatchViewDto view,
        IReadOnlyList<LocalCatchPhotographModel> photographs,
        DateTimeOffset now)
    {
        return new LocalCatchRecordModel
        {
            Id = view.Id,
            UserId = userId,
            TripId = view.TripId,
            CaughtByUserId = view.CaughtByUserId,
            RecordedByUserId = view.RecordedByUserId,
            CaughtOn = view.CaughtOn,
            SpeciesName = view.SpeciesName,
            Weight = (double?)view.Weight,
            Length = (double?)view.Length,
            Method = view.Method,
            BaitOrLure = view.BaitOrLure,
            Notes = view.Notes,
            PlaceName = view.PlaceName,
            Location = ToLocation(userId, view),
            Revision = 1,
            Lifecycle = LocalCatchLifecycleEnum.Saved,
            CreatedOn = now,
            UpdatedOn = now,
            Photographs = photographs
        };
    }

    private static LocalCatchLocationModel? ToLocation(Guid userId, CatchViewDto view)
    {
        var exposure = view.Location;
        if (view.CaughtByUserId != userId
            || exposure is not { Mode: LocationDefaults.ExposureExact, Latitude: { } latitude, Longitude: { } longitude, CapturedOn: { } capturedOn, Source: { } source })
        {
            return null;
        }

        return new LocalCatchLocationModel
        {
            Latitude = latitude,
            Longitude = longitude,
            AccuracyMetres = exposure.AccuracyMetres,
            CapturedOn = capturedOn,
            Source = source,
            Visibility = exposure.Visibility,
            ConsentVersion = LocationDefaults.ConsentVersion
        };
    }
}
