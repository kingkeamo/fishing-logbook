using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Services;

namespace FishingLogBook.Web.Offline;

public sealed class TestCatchSynchroniser : ITestCatchSynchroniser
{
    private readonly ITestCatchStore _store;
    private readonly ITestCatchClient _client;
    private readonly INetworkStatus _networkStatus;

    public TestCatchSynchroniser(
        ITestCatchStore store,
        ITestCatchClient client,
        INetworkStatus networkStatus)
    {
        _store = store;
        _client = client;
        _networkStatus = networkStatus;
    }

    public async Task SynchronisePendingAsync(CancellationToken cancellationToken)
    {
        if (!await _networkStatus.IsOnlineAsync(cancellationToken))
        {
            return;
        }

        await MergeFromServerAsync(cancellationToken);

        var local = await _store.GetAllAsync(cancellationToken);
        foreach (var testCatch in local.Where(NeedsSync))
        {
            await _store.SaveAsync(testCatch with { SyncStatus = SyncStatus.WaitingToSynchronise }, cancellationToken);
        }

        local = await _store.GetAllAsync(cancellationToken);
        foreach (var testCatch in local.Where(NeedsSync))
        {
            await SynchroniseOneAsync(testCatch, cancellationToken);
        }
    }

    public async Task RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        var local = await _store.GetAllAsync(cancellationToken);
        var testCatch = local.SingleOrDefault(item => item.Id == id);
        if (testCatch is null || !NeedsSync(testCatch))
        {
            return;
        }

        if (!await _networkStatus.IsOnlineAsync(cancellationToken))
        {
            await _store.SaveAsync(testCatch with { SyncStatus = SyncStatus.WaitingToSynchronise }, cancellationToken);
            return;
        }

        await SynchroniseOneAsync(testCatch, cancellationToken);
    }

    private async Task MergeFromServerAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TestCatchDto> remote;
        try
        {
            remote = await _client.GetAllAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return;
        }
        catch (TaskCanceledException)
        {
            return;
        }

        var local = await _store.GetAllAsync(cancellationToken);
        var localById = local.ToDictionary(item => item.Id);

        foreach (var dto in remote)
        {
            if (localById.TryGetValue(dto.Id, out var existing) && NeedsSync(existing))
            {
                continue;
            }

            await _store.SaveAsync(
                new TestCatch(dto.Id, dto.SpeciesName, dto.CaughtOn, dto.Notes, SyncStatus.Synchronised),
                cancellationToken);
        }
    }

    private async Task SynchroniseOneAsync(TestCatch testCatch, CancellationToken cancellationToken)
    {
        await _store.SaveAsync(testCatch with { SyncStatus = SyncStatus.Synchronising }, cancellationToken);

        try
        {
            await _client.UpsertAsync(
                new TestCatchDto(testCatch.Id, testCatch.SpeciesName, testCatch.CaughtOn, testCatch.Notes),
                cancellationToken);
            await _store.SaveAsync(testCatch with { SyncStatus = SyncStatus.Synchronised }, cancellationToken);
        }
        catch (HttpRequestException)
        {
            await _store.SaveAsync(testCatch with { SyncStatus = SyncStatus.FailedToSynchronise }, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            await _store.SaveAsync(testCatch with { SyncStatus = SyncStatus.FailedToSynchronise }, cancellationToken);
        }
    }

    private static bool NeedsSync(TestCatch testCatch)
    {
        return testCatch.SyncStatus is SyncStatus.SavedLocally
            or SyncStatus.WaitingToSynchronise
            or SyncStatus.FailedToSynchronise;
    }
}
