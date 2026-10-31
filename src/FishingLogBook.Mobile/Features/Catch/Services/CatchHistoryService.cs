using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Catch.Enums;
using FishingLogBook.Mobile.Features.Catch.Mappers;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Catch.Services;

public sealed class CatchHistoryService(
    ICatchStore catchStore,
    ICatchSyncStatusStore syncStatusStore,
    ICatchRemoteClient remoteClient,
    IConnectivityService connectivity,
    IAccountSessionCoordinator sessionCoordinator,
    CatchPresentationMapper mapper) : ICatchHistoryService
{
    public async Task<CatchHistoryModel> LoadAsync(UiTextService text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lease = sessionCoordinator.Capture();
        if (lease is null)
        {
            return CatchHistoryModel.None;
        }

        var local = await catchStore.ListAsync(cancellationToken);
        var statuses = await syncStatusStore.ListAsync(lease.UserId, cancellationToken);
        var (remoteState, remote) = await LoadRemoteAsync(cancellationToken);
        if (!sessionCoordinator.IsCurrent(lease))
        {
            return CatchHistoryModel.None;
        }

        var remoteById = remote.ToDictionary(item => item.Id);
        var items = new List<CatchPresentationModel>();
        var localIds = new HashSet<Guid>();
        foreach (var record in local)
        {
            statuses.TryGetValue(record.Id, out var status);
            var confirmedOnServer = record.Lifecycle == LocalCatchLifecycleEnum.Saved
                && status?.State == CatchSyncStateEnum.Synced
                && remoteById.ContainsKey(record.Id);
            if (confirmedOnServer)
            {
                continue;
            }

            localIds.Add(record.Id);
            items.Add(mapper.Map(record, text, status));
        }

        items.AddRange(remote.Where(item => !localIds.Contains(item.Id)).Select(item => mapper.MapRemote(item, text)));
        return new CatchHistoryModel(items.OrderByDescending(item => item.RecordedAt).ToArray(), remoteState);
    }

    private async Task<(RemoteCatchStateEnum State, IReadOnlyList<CatchViewDto> Items)> LoadRemoteAsync(
        CancellationToken cancellationToken)
    {
        if (!connectivity.IsOnline)
        {
            return (RemoteCatchStateEnum.Offline, []);
        }

        var result = await remoteClient.ListAsync(cancellationToken);
        if (result.IsSuccess)
        {
            return (RemoteCatchStateEnum.Loaded, result.Value!);
        }

        return (result.Outcome == ApiOutcomeEnum.SessionExpired
            ? RemoteCatchStateEnum.SessionExpired
            : RemoteCatchStateEnum.Unavailable, []);
    }
}
