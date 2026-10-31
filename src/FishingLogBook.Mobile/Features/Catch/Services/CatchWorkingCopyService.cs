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
    TimeProvider timeProvider,
    ILogger<CatchWorkingCopyService> logger) : ICatchWorkingCopyService
{
    public async Task<WorkingCopyOutcomeEnum> EnsureAsync(Guid catchId, CancellationToken cancellationToken)
    {
        var lease = sessionCoordinator.Capture();
        if (lease is null)
        {
            return WorkingCopyOutcomeEnum.SessionExpired;
        }

        if (await catchStore.GetAsync(catchId, cancellationToken) is not null)
        {
            return WorkingCopyOutcomeEnum.Ready;
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
        if (view.RecordedByUserId != lease.UserId)
        {
            return WorkingCopyOutcomeEnum.NotEditable;
        }

        var photographs = await DownloadAsync(lease.UserId, view, cancellationToken);
        return photographs is null
            ? WorkingCopyOutcomeEnum.PhotosUnavailable
            : await CreateAsync(view, photographs, lease, cancellationToken);
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
                await RollbackAsync(lease.UserId, view.Id, photographs);
                return WorkingCopyOutcomeEnum.SessionExpired;
            }

            var record = ToRecord(lease.UserId, view, photographs, now);
            var created = await workingCopyStore.TryCreateAsync(record, now, cancellationToken);
            if (!created && await catchStore.GetAsync(view.Id, cancellationToken) is null)
            {
                await RollbackAsync(lease.UserId, view.Id, photographs);
                return WorkingCopyOutcomeEnum.PhotosUnavailable;
            }

            return WorkingCopyOutcomeEnum.Ready;
        }
        catch (OperationCanceledException)
        {
            await RollbackAsync(lease.UserId, view.Id, photographs);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create the working copy of catch {CatchId}.", view.Id);
            await RollbackAsync(lease.UserId, view.Id, photographs);
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
                ? await fileStore.SaveAsync(userId, photograph.Id, photograph.ContentType, download.Bytes!, cancellationToken)
                : null;
            if (relativePath is null)
            {
                await RollbackAsync(userId, view.Id, saved);
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

    private async Task RollbackAsync(Guid userId, Guid catchId, IReadOnlyList<LocalCatchPhotographModel> photographs)
    {
        if (await catchStore.GetAsync(catchId, CancellationToken.None) is not null)
        {
            return;
        }

        foreach (var path in photographs.Select(photograph => photograph.LocalFilePath).OfType<string>())
        {
            fileStore.TryDelete(userId, path);
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
