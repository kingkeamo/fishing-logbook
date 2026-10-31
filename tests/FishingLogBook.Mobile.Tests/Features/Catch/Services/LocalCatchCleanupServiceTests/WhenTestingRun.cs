using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
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

public sealed class WhenTestingRun : BaseSqliteCatchStoreTest
{
    private readonly ICatchRemoteClient MockRemote = Substitute.For<ICatchRemoteClient>();
    private readonly IPhotographFileStore MockFiles = Substitute.For<IPhotographFileStore>();
    private readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    private readonly ActiveCatchEditRegistry _edits = new();
    private readonly SqliteLocalCatchCleanupStore _cleanup;

    public WhenTestingRun()
    {
        _cleanup = new SqliteLocalCatchCleanupStore(ConnectionFactory, Database);
        MockConnectivity.IsOnline.Returns(true);
        MockFiles.TryDelete(OwnerUserId, Arg.Any<string>()).Returns(true);
    }

    private LocalCatchCleanupService CreateService()
    {
        return new LocalCatchCleanupService(_cleanup, MockRemote, MockFiles, _edits,
            MockSessionCoordinator, MockConnectivity, MockNotifier, TimeProvider.System, Sut,
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
        MockRemote.GetAsync(record.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<CatchViewDto>.Succeeded(ToRemote(record)));
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
            Photographs = record.Photographs.Select(photo => new CatchPhotographViewDto(photo.Id, photo.ContentType!, "https://photos.example/durable")).ToArray()
        };
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public async Task ItShouldAllowOnlyDatabaseMicrosecondRoundingWhenConfirmingTheCatchTime(long ticks, bool deleted)
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        MockRemote.GetAsync(record.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<CatchViewDto>.Succeeded(
            ToRemote(record) with { CaughtOn = record.CaughtOn.AddTicks(ticks) }));
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        ((await Sut.GetAsync(record.Id, CancellationToken.None)) is null).Should().Be(deleted);
    }

    [Theory]
    [InlineData("UPDATE outboxoperations SET status='Pending';")]
    [InlineData("UPDATE outboxoperations SET status='InProgress';")]
    [InlineData("UPDATE outboxoperations SET status='Failed';")]
    [InlineData("UPDATE localphotographsyncs SET status='Uploaded';")]
    [InlineData("UPDATE localcatchsyncs SET syncedrevision=0;")]
    [InlineData("UPDATE localcatches SET lifecycle='Draft';")]
    public async Task ItShouldNeverOfferUnconfirmedOrActiveDraftDataForCleanup(string sql)
    {
        // Arrange
        await CreateConfirmedAsync();
        await Database.ExecuteInTransactionAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(token);
        }, CancellationToken.None);
        // Act
        var candidates = await _cleanup.ListCandidatesAsync(OwnerUserId, CancellationToken.None);
        // Assert
        candidates.Should().BeEmpty();
        (await Sut.ListAsync(CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldRecheckAnEditorOpenedDuringTheServerRead()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        IDisposable? edit = null;
        MockRemote.GetAsync(record.Id, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            edit = await _edits.AcquireAsync(record.Id, CancellationToken.None);
            return ApiResultModel<CatchViewDto>.Succeeded(ToRemote(record));
        });
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await Sut.GetAsync(record.Id, CancellationToken.None)).Should().NotBeNull();
        MockFiles.DidNotReceive().TryDelete(Arg.Any<Guid>(), Arg.Any<string>());
        edit!.Dispose();
    }

    [Fact]
    public async Task ItShouldNotDeleteAfterAnAccountSwitchDuringTheServerRead()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        MockRemote.GetAsync(record.Id, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            MockSessionCoordinator.IsCurrent(OwnerLease).Returns(false);
            return ApiResultModel<CatchViewDto>.Succeeded(ToRemote(record));
        });
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await Sut.GetForOwnerAsync(OwnerUserId, record.Id, CancellationToken.None)).Should().NotBeNull();
        MockFiles.DidNotReceive().TryDelete(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldCleanAConfirmedWorkingCopyWhenTheEditorClosesWithoutTheListBeingOpen()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        var edit = await _edits.AcquireAsync(record.Id, CancellationToken.None);
        using var service = CreateService();
        service.Start();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MockNotifier.When(notifier => notifier.NotifyCatchesChanged()).Do(_ => finished.TrySetResult());
        // Act
        edit.Dispose();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // Assert
        (await Sut.GetAsync(record.Id, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRemoveOnlyTheConfirmedRevisionAndDrainItsFilesIdempotently()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await Sut.GetAsync(record.Id, CancellationToken.None)).Should().BeNull();
        (await _cleanup.ListPendingFileDeletionsAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
        MockFiles.Received(1).TryDelete(OwnerUserId, record.Photographs[0].LocalFilePath!);
        await MockRemote.Received(1).GetAsync(record.Id, Arg.Any<CancellationToken>());
        MockNotifier.Received(1).NotifyCatchesChanged();
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("missing-photo")]
    [InlineData("missing-bytes")]
    [InlineData("503")]
    [InlineData("401")]
    public async Task ItShouldRetainDataWhenServerConfirmationIsIncomplete(string failure)
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        var view = ToRemote(record);
        var result = failure switch
        {
            "metadata" => ApiResultModel<CatchViewDto>.Succeeded(view with { Notes = "Old revision" }),
            "missing-photo" => ApiResultModel<CatchViewDto>.Succeeded(view with { Photographs = [] }),
            "missing-bytes" => ApiResultModel<CatchViewDto>.Succeeded(view with { Photographs = [view.Photographs[0] with { Url = null }] }),
            "401" => ApiResultModel<CatchViewDto>.Failed(ApiOutcomeEnum.SessionExpired),
            _ => ApiResultModel<CatchViewDto>.Failed(ApiOutcomeEnum.Unavailable)
        };
        MockRemote.GetAsync(record.Id, Arg.Any<CancellationToken>()).Returns(result);
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await Sut.GetAsync(record.Id, CancellationToken.None)).Should().NotBeNull();
        MockFiles.DidNotReceive().TryDelete(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldNotCleanAnActiveEditor()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        using var edit = await _edits.AcquireAsync(record.Id, CancellationToken.None);
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await Sut.GetAsync(record.Id, CancellationToken.None)).Should().NotBeNull();
        await MockRemote.DidNotReceive().GetAsync(record.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetainANewerEditMadeWhileConfirmationIsInFlight()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        MockRemote.GetAsync(record.Id, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await Sut.SaveAsync(CreateSaveArgs(record.Id, speciesName: "Pike"), CancellationToken.None);
            return ApiResultModel<CatchViewDto>.Succeeded(ToRemote(record));
        });
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        var current = (await Sut.GetAsync(record.Id, CancellationToken.None))!;
        current.Revision.Should().BeGreaterThan(record.Revision);
        current.SpeciesName.Should().Be("Pike");
        MockFiles.DidNotReceive().TryDelete(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldRecheckEligibilityInTheDeletionTransaction()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        var candidate = (await _cleanup.ListCandidatesAsync(OwnerUserId, CancellationToken.None)).Single();
        await Sut.SaveAsync(CreateSaveArgs(record.Id), CancellationToken.None);
        // Act
        var deleted = await _cleanup.TryDeleteAsync(candidate, DateTimeOffset.UtcNow, CancellationToken.None);
        // Assert
        deleted.Should().BeFalse();
        (await Sut.GetAsync(record.Id, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldRecoverQueuedFilesOfflineAfterRestartAndRetryFailedDeletion()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        var candidate = (await _cleanup.ListCandidatesAsync(OwnerUserId, CancellationToken.None)).Single();
        await _cleanup.TryDeleteAsync(candidate, DateTimeOffset.UtcNow, CancellationToken.None);
        MockConnectivity.IsOnline.Returns(false);
        MockFiles.TryDelete(OwnerUserId, candidate.RelativeFilePaths[0]).Returns(false, true);
        // Act
        using (var first = CreateService())
        {
            await first.RunAsync(CancellationToken.None);
        }
        // Assert
        (await _cleanup.ListPendingFileDeletionsAsync(OwnerUserId, CancellationToken.None)).Should().ContainSingle();
        // Act
        using var restarted = CreateService();
        await restarted.RunAsync(CancellationToken.None);
        // Assert
        (await _cleanup.ListPendingFileDeletionsAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
        await MockRemote.DidNotReceive().GetAsync(record.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetainAQueuedPathThatHasBeenReferencedAgain()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        var path = record.Photographs[0].LocalFilePath!;
        await _cleanup.QueueFileDeletionAsync(OwnerUserId, path, DateTimeOffset.UtcNow, CancellationToken.None);
        var queued = (await _cleanup.ListPendingFileDeletionsAsync(OwnerUserId, CancellationToken.None)).Single();
        // Act
        await _cleanup.TryDeleteFileAsync(OwnerUserId, queued.Id, path, () => MockFiles.TryDelete(OwnerUserId, path), CancellationToken.None);
        // Assert
        MockFiles.DidNotReceive().TryDelete(OwnerUserId, path);
        (await _cleanup.ListPendingFileDeletionsAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldLeaveAnotherAccountsQueueUntouched()
    {
        // Arrange
        await _cleanup.QueueFileDeletionAsync(OtherUserId, "other/file.jpg", DateTimeOffset.UtcNow, CancellationToken.None);
        MockConnectivity.IsOnline.Returns(false);
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await _cleanup.ListPendingFileDeletionsAsync(OtherUserId, CancellationToken.None)).Should().ContainSingle();
        MockFiles.DidNotReceive().TryDelete(OtherUserId, Arg.Any<string>());
    }
}
