using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Services;

namespace FishingLogBook.Web.Offline;

public sealed class TestCatchSynchroniser : ITestCatchSynchroniser
{
    private readonly ITestCatchStore _store;
    private readonly ITestCatchPhotoStore _photoStore;
    private readonly ITestCatchClient _client;
    private readonly INetworkStatus _networkStatus;

    public TestCatchSynchroniser(
        ITestCatchStore store,
        ITestCatchPhotoStore photoStore,
        ITestCatchClient client,
        INetworkStatus networkStatus)
    {
        _store = store;
        _photoStore = photoStore;
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
        foreach (var testCatch in local.Where(NeedsCatchSync))
        {
            await _store.SaveAsync(testCatch with { SyncStatus = SyncStatus.WaitingToSynchronise }, cancellationToken);
        }

        local = await _store.GetAllAsync(cancellationToken);
        foreach (var testCatch in local.Where(NeedsCatchSync))
        {
            await SynchroniseCatchAsync(testCatch, cancellationToken);
        }

        local = await _store.GetAllAsync(cancellationToken);
        foreach (var testCatch in local.Where(catchItem => catchItem.SyncStatus == SyncStatus.Synchronised && NeedsPhotoSync(catchItem)))
        {
            await SynchronisePhotographAsync(testCatch, cancellationToken);
        }
    }

    public async Task RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        var local = await _store.GetAllAsync(cancellationToken);
        var testCatch = local.SingleOrDefault(item => item.Id == id);
        if (testCatch is null || !NeedsCatchSync(testCatch))
        {
            return;
        }

        if (!await _networkStatus.IsOnlineAsync(cancellationToken))
        {
            await _store.SaveAsync(testCatch with { SyncStatus = SyncStatus.WaitingToSynchronise }, cancellationToken);
            return;
        }

        await SynchroniseCatchAsync(testCatch, cancellationToken);
        local = await _store.GetAllAsync(cancellationToken);
        testCatch = local.SingleOrDefault(item => item.Id == id);
        if (testCatch is not null && testCatch.SyncStatus == SyncStatus.Synchronised && NeedsPhotoSync(testCatch))
        {
            await SynchronisePhotographAsync(testCatch, cancellationToken);
        }
    }

    public async Task RetryPhotographAsync(Guid id, CancellationToken cancellationToken)
    {
        var local = await _store.GetAllAsync(cancellationToken);
        var testCatch = local.SingleOrDefault(item => item.Id == id);
        if (testCatch is null || !NeedsPhotoSync(testCatch))
        {
            return;
        }

        if (!await _networkStatus.IsOnlineAsync(cancellationToken))
        {
            await SavePhotographStatusAsync(testCatch, SyncStatus.WaitingToSynchronise, cancellationToken);
            return;
        }

        if (NeedsCatchSync(testCatch))
        {
            return;
        }

        await SynchronisePhotographAsync(testCatch, cancellationToken);
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
            localById.TryGetValue(dto.Id, out var existing);
            if (existing is not null && NeedsCatchSync(existing))
            {
                continue;
            }

            var photograph = existing is not null && NeedsPhotoSync(existing)
                ? existing.Photograph
                : FromRemotePhotograph(dto);

            await _store.SaveAsync(
                new TestCatch(
                    dto.Id,
                    dto.SpeciesName,
                    dto.CaughtOn,
                    dto.Notes,
                    SyncStatus.Synchronised,
                    photograph),
                cancellationToken);
        }
    }

    private async Task SynchroniseCatchAsync(TestCatch testCatch, CancellationToken cancellationToken)
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

    private async Task SynchronisePhotographAsync(TestCatch testCatch, CancellationToken cancellationToken)
    {
        var photograph = testCatch.Photograph;
        if (photograph is null)
        {
            return;
        }

        var bytes = await _photoStore.GetAsync(testCatch.Id, cancellationToken);
        if (bytes is null)
        {
            return;
        }

        await SavePhotographStatusAsync(testCatch, SyncStatus.Synchronising, cancellationToken);

        try
        {
            var upload = await _client.CreatePhotographUploadAsync(
                testCatch.Id,
                new PhotographUploadRequestDto(photograph.Id, photograph.ContentType),
                cancellationToken);
            await _client.UploadPhotographAsync(upload.UploadUrl, bytes.Bytes, photograph.ContentType, cancellationToken);
            await _client.RecordPhotographAsync(
                testCatch.Id,
                new RecordPhotographDto(photograph.Id, upload.ObjectKey, photograph.ContentType),
                cancellationToken);
            await _store.SaveAsync(
                testCatch with
                {
                    SyncStatus = SyncStatus.Synchronised,
                    Photograph = photograph with
                    {
                        SyncStatus = SyncStatus.Synchronised,
                        ObjectKey = upload.ObjectKey
                    }
                },
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            await SavePhotographStatusAsync(testCatch with { SyncStatus = SyncStatus.Synchronised }, SyncStatus.FailedToSynchronise, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            await SavePhotographStatusAsync(testCatch with { SyncStatus = SyncStatus.Synchronised }, SyncStatus.FailedToSynchronise, cancellationToken);
        }
    }

    private Task SavePhotographStatusAsync(TestCatch testCatch, SyncStatus photoStatus, CancellationToken cancellationToken)
    {
        if (testCatch.Photograph is null)
        {
            return Task.CompletedTask;
        }

        return _store.SaveAsync(
            testCatch with { Photograph = testCatch.Photograph with { SyncStatus = photoStatus } },
            cancellationToken);
    }

    private static TestCatchPhotograph? FromRemotePhotograph(TestCatchDto dto)
    {
        if (dto.PhotographId is null || string.IsNullOrWhiteSpace(dto.PhotographUrl))
        {
            return null;
        }

        return new TestCatchPhotograph(
            dto.PhotographId.Value,
            dto.PhotographContentType ?? "image/jpeg",
            SyncStatus.Synchronised,
            RemoteUrl: dto.PhotographUrl);
    }

    private static bool NeedsCatchSync(TestCatch testCatch)
    {
        return testCatch.SyncStatus is SyncStatus.SavedLocally
            or SyncStatus.WaitingToSynchronise
            or SyncStatus.FailedToSynchronise;
    }

    private static bool NeedsPhotoSync(TestCatch testCatch)
    {
        return testCatch.Photograph?.SyncStatus is SyncStatus.SavedLocally
            or SyncStatus.WaitingToSynchronise
            or SyncStatus.FailedToSynchronise;
    }
}
