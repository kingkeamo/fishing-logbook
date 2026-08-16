using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.SystemStatus.Services;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Services;

namespace FishingLogBook.Web.Features.TestCatch.Offline;

public sealed class TestCatchSynchroniser : ITestCatchSynchroniser
{
    private readonly ITestCatchStore _store;
    private readonly ITestCatchPhotoStore _photoStore;
    private readonly ITestCatchClient _client;
    private readonly INetworkService _networkStatus;
    private readonly IDiagnosticLogger? _diagnostics;
    private readonly ILoggingService? _logging;

    public TestCatchSynchroniser(
        ITestCatchStore store,
        ITestCatchPhotoStore photoStore,
        ITestCatchClient client,
        INetworkService networkStatus,
        IDiagnosticLogger? diagnostics = null,
        ILoggingService? logging = null)
    {
        _store = store;
        _photoStore = photoStore;
        _client = client;
        _networkStatus = networkStatus;
        _diagnostics = diagnostics;
        _logging = logging;
    }

    public async Task SynchronisePendingAsync(CancellationToken cancellationToken)
    {
        if (!await _networkStatus.IsOnlineAsync(cancellationToken))
        {
            return;
        }

        await SafeLogAsync(DiagnosticLevel.Information, DiagnosticEventNames.SyncStarted, "Catch synchronisation started.", cancellationToken);
        try
        {
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

            await SafeLogAsync(DiagnosticLevel.Information, DiagnosticEventNames.SyncCompleted, "Catch synchronisation completed.", cancellationToken);
        }
        catch (Exception exception)
        {
            await SafeLogAsync(DiagnosticLevel.Error, DiagnosticEventNames.SyncFailed, "Catch synchronisation failed.", cancellationToken, exception);
            throw;
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

        await SafeLogAsync(DiagnosticLevel.Information, DiagnosticEventNames.SyncRetry, "Catch synchronisation retry started.", cancellationToken);
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
                new TestCatchModel(
                    dto.Id,
                    dto.SpeciesName,
                    dto.CaughtOn,
                    dto.Notes,
                    SyncStatus.Synchronised,
                    photograph,
                    FromRemoteLocation(dto.Location)),
                cancellationToken);
        }
    }

    private async Task SynchroniseCatchAsync(TestCatchModel testCatch, CancellationToken cancellationToken)
    {
        await _store.SaveAsync(testCatch with { SyncStatus = SyncStatus.Synchronising }, cancellationToken);

        try
        {
            await _client.UpsertAsync(
                new TestCatchDto(
                    testCatch.Id,
                    testCatch.SpeciesName,
                    testCatch.CaughtOn,
                    testCatch.Notes,
                    Location: ToRemoteLocation(testCatch.Location)),
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

    private async Task SynchronisePhotographAsync(TestCatchModel testCatch, CancellationToken cancellationToken)
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

    private Task SavePhotographStatusAsync(TestCatchModel testCatch, SyncStatus photoStatus, CancellationToken cancellationToken)
    {
        if (testCatch.Photograph is null)
        {
            return Task.CompletedTask;
        }

        return _store.SaveAsync(
            testCatch with { Photograph = testCatch.Photograph with { SyncStatus = photoStatus } },
            cancellationToken);
    }

    private static CatchLocationDto? ToRemoteLocation(TestCatchLocationModel? location)
    {
        if (location is null)
        {
            return null;
        }

        return new CatchLocationDto(
            location.Latitude,
            location.Longitude,
            location.AccuracyMetres,
            location.CapturedOn,
            location.Source,
            location.Visibility,
            location.ConsentVersion);
    }

    private static TestCatchLocationModel? FromRemoteLocation(CatchLocationDto? location)
    {
        if (location is null)
        {
            return null;
        }

        return new TestCatchLocationModel(
            location.Latitude,
            location.Longitude,
            location.AccuracyMetres,
            location.CapturedOn,
            location.Source,
            location.Visibility,
            location.ConsentVersion);
    }

    private static TestCatchPhotographModel? FromRemotePhotograph(TestCatchDto dto)
    {
        if (dto.PhotographId is null || string.IsNullOrWhiteSpace(dto.PhotographUrl))
        {
            return null;
        }

        return new TestCatchPhotographModel(
            dto.PhotographId.Value,
            dto.PhotographContentType ?? "image/jpeg",
            SyncStatus.Synchronised,
            RemoteUrl: dto.PhotographUrl);
    }

    private static bool NeedsCatchSync(TestCatchModel testCatch)
    {
        return testCatch.SyncStatus is SyncStatus.SavedLocally
            or SyncStatus.WaitingToSynchronise
            or SyncStatus.FailedToSynchronise;
    }

    private static bool NeedsPhotoSync(TestCatchModel testCatch)
    {
        return testCatch.Photograph?.SyncStatus is SyncStatus.SavedLocally
            or SyncStatus.WaitingToSynchronise
            or SyncStatus.FailedToSynchronise;
    }

    private async Task SafeLogAsync(
        DiagnosticLevel level,
        string eventName,
        string message,
        CancellationToken cancellationToken,
        Exception? exception = null)
    {
        if (_diagnostics is null)
        {
            return;
        }

        try
        {
            await _diagnostics.LogAsync(level, eventName, message, exception: exception, cancellationToken: cancellationToken);
        }
        catch (Exception loggingException)
        {
            if (_logging is not null)
            {
                await _logging.LogErrorAsync("diagnostic log", loggingException, cancellationToken);
            }
        }
    }
}
