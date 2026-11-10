using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchStoreTests;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Services.LocalCatchCleanupServiceTests;

public sealed class WhenTestingRunVariations : BaseSqliteCatchStoreTest
{
    private readonly ICatchRemoteClient _mockRemote = Substitute.For<ICatchRemoteClient>();
    private readonly IPhotographFileStore _mockFiles = Substitute.For<IPhotographFileStore>();
    private readonly IConnectivityService _mockConnectivity = Substitute.For<IConnectivityService>();
    private readonly IActiveCatchEditRegistry _mockEdits = Substitute.For<IActiveCatchEditRegistry>();
    private readonly SqliteLocalCatchCleanupStore _cleanup;

    public WhenTestingRunVariations()
    {
        _cleanup = new SqliteLocalCatchCleanupStore(ConnectionFactory, Database);
        _mockConnectivity.IsOnline.Returns(true);
        _mockFiles.TryDelete(OwnerUserId, Arg.Any<string>()).Returns(true);
        _mockEdits.IsActive(Arg.Any<Guid>()).Returns(false);
        _mockEdits.RunIfInactiveAsync(Arg.Any<Guid?>(), Arg.Any<Func<Task<bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<Task<bool>>>()());
    }

    private LocalCatchCleanupService CreateService()
    {
        return new LocalCatchCleanupService(
            _cleanup,
            _mockRemote,
            _mockFiles,
            _mockEdits,
            MockSessionCoordinator,
            _mockConnectivity,
            MockNotifier,
            TimeProvider.System,
            Sut,
            NullLogger<LocalCatchCleanupService>.Instance);
    }

    private async Task<LocalCatchRecordModel> CreateConfirmedAsync()
    {
        var args = CreateSaveArgs();
        await Sut.SaveAsync(args, CancellationToken.None);
        var record = (await Sut.GetAsync(args.Id, CancellationToken.None))!;
        await Database.ExecuteInTransactionAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE outboxoperations SET status = 'Completed';
                INSERT INTO localcatchsyncs (catchid, userid, syncedrevision, syncedon)
                SELECT id, userid, revision, updatedon FROM localcatches;
                INSERT INTO localphotographsyncs (photographid, catchid, userid, status, updatedon)
                SELECT id, catchid, userid, 'Recorded', createdon FROM localcatchphotographs;
                """;
            await command.ExecuteNonQueryAsync(token);
        }, CancellationToken.None);
        _mockRemote.GetAsync(record.Id, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<CatchViewDto>.Succeeded(ToRemote(record)));
        return record;
    }

    private static CatchViewDto ToRemote(LocalCatchRecordModel record)
    {
        return new CatchViewDto(record.Id, record.CaughtByUserId, record.CaughtOn)
        {
            RecordedByUserId = record.RecordedByUserId,
            TripId = record.TripId,
            SpeciesName = record.SpeciesName,
            Weight = (decimal?)record.Weight,
            Length = (decimal?)record.Length,
            Method = record.Method,
            BaitOrLure = record.BaitOrLure,
            Notes = record.Notes,
            PlaceName = record.PlaceName,
            Photographs = record.Photographs
                .Select(photo => new CatchPhotographViewDto(photo.Id, photo.ContentType!, "https://photos.example/durable"))
                .ToArray()
        };
    }

    public static TheoryData<string, Func<CatchViewDto, CatchViewDto>> MismatchingServerCatches
    {
        get
        {
            return new TheoryData<string, Func<CatchViewDto, CatchViewDto>>
            {
                { "id", remote => remote with { Id = Guid.NewGuid() } },
                { "angler", remote => remote with { CaughtByUserId = Guid.NewGuid() } },
                { "recorder", remote => remote with { RecordedByUserId = Guid.NewGuid() } },
                { "trip", remote => remote with { TripId = Guid.NewGuid() } },
                { "species", remote => remote with { SpeciesName = "Pike" } },
                { "weight", remote => remote with { Weight = 99m } },
                { "length", remote => remote with { Length = 1m } },
                { "method", remote => remote with { Method = "Spinning" } },
                { "bait", remote => remote with { BaitOrLure = "Worm" } },
                { "notes", remote => remote with { Notes = "Different" } },
                { "place", remote => remote with { PlaceName = "Elsewhere" } },
                { "caught on", remote => remote with { CaughtOn = remote.CaughtOn.AddMinutes(5) } },
                { "no photographs", remote => remote with { Photographs = [] } },
                { "unknown photograph", remote => remote with { Photographs = [new CatchPhotographViewDto(Guid.NewGuid(), "image/jpeg", "https://x/p")] } },
                { "photograph without a url", remote => remote with { Photographs = [remote.Photographs[0] with { Url = " " }] } }
            };
        }
    }

    [Theory]
    [MemberData(nameof(MismatchingServerCatches))]
    public async Task ItShouldKeepTheLocalCatchWhenTheServerCopyDiffersInAnyField(
        string field,
        Func<CatchViewDto, CatchViewDto> change)
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        _mockRemote.GetAsync(record.Id, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<CatchViewDto>.Succeeded(change(ToRemote(record))));
        using var service = CreateService();

        // Act
        await service.RunAsync(CancellationToken.None);

        // Assert
        (await Sut.GetAsync(record.Id, CancellationToken.None)).Should().NotBeNull($"a differing {field} must not allow deletion");
    }

    [Fact]
    public async Task ItShouldOnlyDrainQueuedFilesWhileOffline()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        _mockConnectivity.IsOnline.Returns(false);
        using var service = CreateService();

        // Act
        await service.RunAsync(CancellationToken.None);

        // Assert
        (await Sut.GetAsync(record.Id, CancellationToken.None)).Should().NotBeNull();
        await _mockRemote.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDoNothingWithoutASession()
    {
        // Arrange
        await CreateConfirmedAsync();
        MockSessionCoordinator.Capture().Returns((AccountSessionLeaseModel?)null);
        using var service = CreateService();

        // Act
        await service.RunAsync(CancellationToken.None);

        // Assert
        await _mockRemote.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheCatchWhenTheServerCannotConfirmIt()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        _mockRemote.GetAsync(record.Id, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<CatchViewDto>.Failed(FishingLogBook.Mobile.Api.Enums.ApiOutcomeEnum.ServerError));
        using var service = CreateService();

        // Act
        await service.RunAsync(CancellationToken.None);

        // Assert
        (await Sut.GetAsync(record.Id, CancellationToken.None)).Should().NotBeNull();
        MockNotifier.DidNotReceive().NotifyCatchesChanged();
    }

    [Fact]
    public async Task ItShouldNotifyListenersWhenACatchWasCleanedUp()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        using var service = CreateService();

        // Act
        await service.RunAsync(CancellationToken.None);

        // Assert
        (await Sut.GetAsync(record.Id, CancellationToken.None)).Should().BeNull();
        MockNotifier.Received(1).NotifyCatchesChanged();
    }

    [Fact]
    public async Task ItShouldSwallowAFailureFromTheServerAndKeepRunning()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        _mockRemote.GetAsync(record.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ApiResultModel<CatchViewDto>>(new InvalidOperationException("boom")));
        using var service = CreateService();

        // Act
        var act = () => service.RunAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        (await Sut.GetAsync(record.Id, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldTreatACancelledRunAsNotAFailure()
    {
        // Arrange
        await CreateConfirmedAsync();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var service = CreateService();

        // Act
        var act = () => service.RunAsync(cts.Token);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ItShouldDoNothingWhenRunAfterBeingDisposed()
    {
        // Arrange
        await CreateConfirmedAsync();
        var service = CreateService();
        service.Dispose();

        // Act
        await service.RunAsync(CancellationToken.None);

        // Assert
        await _mockRemote.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ItShouldRefuseToStartAfterBeingDisposedAndTolerateASecondDispose()
    {
        // Arrange
        var service = CreateService();
        service.Dispose();

        // Act
        service.Dispose();
        var act = service.Start;

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task ItShouldRunWhenASyncRunCompletesAfterStart()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        using var service = CreateService();
        service.Start();
        service.Start();

        // Act
        MockNotifier.SyncRunCompleted += Raise.Event();
        await WaitUntilAsync(async () => await Sut.GetAsync(record.Id, CancellationToken.None) is null);

        // Assert
        (await Sut.GetAsync(record.Id, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRunWhenASessionIsActivated()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        using var service = CreateService();
        service.Start();

        // Act
        MockSessionCoordinator.SessionActivated += Raise.Event();
        await WaitUntilAsync(async () => await Sut.GetAsync(record.Id, CancellationToken.None) is null);

        // Assert
        (await Sut.GetAsync(record.Id, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRunWhenAnEditorIsReleased()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        using var service = CreateService();
        service.Start();

        // Act
        _mockEdits.EditReleased += Raise.Event();
        await WaitUntilAsync(async () => await Sut.GetAsync(record.Id, CancellationToken.None) is null);

        // Assert
        (await Sut.GetAsync(record.Id, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ItShouldStopReactingToTriggersAfterBeingDisposed()
    {
        // Arrange
        await CreateConfirmedAsync();
        var service = CreateService();
        service.Start();
        service.Dispose();

        // Act
        MockNotifier.SyncRunCompleted += Raise.Event();
        await Task.Delay(200);

        // Assert
        await _mockRemote.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRunAgainWhenAnotherRunIsRequestedWhileOneIsInProgress()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var reads = 0;
        _mockRemote.GetAsync(record.Id, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            if (Interlocked.Increment(ref reads) == 1)
            {
                entered.SetResult();
                await release.Task;
            }

            return ApiResultModel<CatchViewDto>.Succeeded(ToRemote(record) with { Notes = "different" });
        });
        using var service = CreateService();

        // Act
        var first = service.RunAsync(CancellationToken.None);
        await entered.Task;
        await service.RunAsync(CancellationToken.None);
        release.SetResult();
        await first;

        // Assert
        reads.Should().Be(2);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!await condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }
}
