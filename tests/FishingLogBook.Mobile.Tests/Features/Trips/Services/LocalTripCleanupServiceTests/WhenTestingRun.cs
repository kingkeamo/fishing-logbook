using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Stores;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Tests.Features.Trips.Offline.Stores.SqliteTripStoreTests;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.LocalTripCleanupServiceTests;

public sealed class WhenTestingRun : BaseSqliteTripStoreTest
{
    private readonly ITripClient MockRemote = Substitute.For<ITripClient>();
    private readonly IPhotographFileStore MockFiles = Substitute.For<IPhotographFileStore>();
    private readonly ActiveTripEditRegistry _edits = new();
    private readonly SqliteLocalTripCleanupStore _cleanup;

    public WhenTestingRun()
    {
        _cleanup = new SqliteLocalTripCleanupStore(ConnectionFactory, Database);
        MockFiles.TryDelete(OwnerUserId, Arg.Any<string>()).Returns(true);
    }

    private LocalTripCleanupService CreateService()
    {
        return new LocalTripCleanupService(
            _cleanup,
            TripStore,
            MockRemote,
            MockFiles,
            _edits,
            MockSessionCoordinator,
            MockConnectivity,
            MockNotifier,
            TimeProvider.System,
            NullLogger<LocalTripCleanupService>.Instance);
    }

    private async Task<LocalTripRecordModel> CreateConfirmedAsync(
        string status = TripConstants.Completed,
        bool withPhotograph = false)
    {
        var tripId = Guid.NewGuid();
        var photograph = new TripPhotographViewDto(Guid.NewGuid(), "image/jpeg", Now, "https://photos.example/trip")
        {
            ContributedByUserId = OwnerUserId
        };
        var trip = NewTrip(tripId) with
        {
            Detail = new TripDetailDto(new TripViewDto(tripId, OwnerUserId, status, Now)
            {
                Title = "Corrib",
                EndedOn = status == TripConstants.Completed ? Now : null,
                PlannedStartOn = status == TripConstants.Cancelled ? Now : null
            })
            {
                Role = TripParticipantConstants.Owner,
                Photographs = withPhotograph ? [photograph] : []
            }
        };
        await TripStore.SaveAsync(OwnerLease, trip, 0, CancellationToken.None);
        if (withPhotograph)
        {
            await TripStore.SaveContributionAsync(OwnerLease, new LocalTripContributionModel
            {
                Id = photograph.Id,
                TripId = tripId,
                UserId = OwnerUserId,
                Photograph = photograph,
                RelativeFilePath = "photos/trip.jpg"
            }, 0, CancellationToken.None);
        }

        await ConfirmLocalAsync();
        var record = (await TripStore.GetAsync(OwnerUserId, tripId, CancellationToken.None))!;
        MockRemote.GetAsync(tripId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(ToRemote(record)));
        return record;
    }

    private Task ConfirmLocalAsync()
    {
        return Database.ExecuteInTransactionAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE outboxoperations SET status = 'Completed';
                UPDATE localtripdocuments SET syncedrevision = revision;
                UPDATE localtripcontributions SET syncedrevision = revision;
                """;
            await command.ExecuteNonQueryAsync(token);
        }, CancellationToken.None);
    }

    private static TripDetailDto ToRemote(LocalTripRecordModel record)
    {
        return record.Detail with
        {
            Photographs = [.. record.Detail.Photographs.Select(photograph => photograph.Url is null
                ? photograph with { Url = "https://photos.example/trip" }
                : photograph)]
        };
    }

    [Theory]
    [InlineData(TripConstants.Completed)]
    [InlineData(TripConstants.Cancelled)]
    public async Task ItShouldRemoveAConfirmedFinishedTrip(string status)
    {
        // Arrange
        var record = await CreateConfirmedAsync(status);
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await TripStore.GetAsync(OwnerUserId, record.Detail.Trip.Id, CancellationToken.None)).Should().BeNull();
        MockNotifier.Received(1).NotifyStateChanged();
    }

    [Theory]
    [InlineData(TripConstants.Active)]
    [InlineData(TripConstants.Planned)]
    public async Task ItShouldRetainAnOpenOrPlannedTrip(string status)
    {
        // Arrange
        await CreateConfirmedAsync(status);
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await TripStore.ListAsync(OwnerUserId, CancellationToken.None)).Should().ContainSingle();
        await MockRemote.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("UPDATE outboxoperations SET status='Pending';")]
    [InlineData("UPDATE outboxoperations SET status='Failed';")]
    [InlineData("UPDATE localtripdocuments SET syncedrevision=0;")]
    public async Task ItShouldNeverOfferUnconfirmedTripDataForCleanup(string sql)
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
        (await TripStore.ListAsync(OwnerUserId, CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldRetainATripThatStillHasALocalCatch()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        await Sut.SaveAsync(CreateSaveArgs(tripId: record.Detail.Trip.Id, tripTitle: "Corrib"), CancellationToken.None);
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await TripStore.GetAsync(OwnerUserId, record.Detail.Trip.Id, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldRetainATripWithAPendingContribution()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        await TripStore.SaveContributionAsync(OwnerLease, NewNote(record.Detail.Trip.Id), 0, CancellationToken.None);
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await TripStore.GetAsync(OwnerUserId, record.Detail.Trip.Id, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldRecheckAnEditorOpenedDuringTheServerRead()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        IDisposable? edit = null;
        MockRemote.GetAsync(record.Detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            edit = await _edits.AcquireAsync(record.Detail.Trip.Id, CancellationToken.None);
            return ApiResultModel<TripDetailDto>.Succeeded(ToRemote(record));
        });
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await TripStore.GetAsync(OwnerUserId, record.Detail.Trip.Id, CancellationToken.None)).Should().NotBeNull();
        MockFiles.DidNotReceive().TryDelete(Arg.Any<Guid>(), Arg.Any<string>());
        edit!.Dispose();
    }

    [Fact]
    public async Task ItShouldNotDeleteAfterAnAccountSwitchDuringTheServerRead()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        MockRemote.GetAsync(record.Detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            MockSessionCoordinator.IsCurrent(OwnerLease).Returns(false);
            return ApiResultModel<TripDetailDto>.Succeeded(ToRemote(record));
        });
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await TripStore.GetAsync(OwnerUserId, record.Detail.Trip.Id, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldCleanAConfirmedTripWhenTheEditorCloses()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        var edit = await _edits.AcquireAsync(record.Detail.Trip.Id, CancellationToken.None);
        using var service = CreateService();
        service.Start();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MockNotifier.When(notifier => notifier.NotifyStateChanged()).Do(_ => finished.TrySetResult());
        // Act
        edit.Dispose();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // Assert
        (await TripStore.GetAsync(OwnerUserId, record.Detail.Trip.Id, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRemoveOnlyTheConfirmedRevisionAndDrainItsFilesIdempotently()
    {
        // Arrange
        var record = await CreateConfirmedAsync(withPhotograph: true);
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await TripStore.GetAsync(OwnerUserId, record.Detail.Trip.Id, CancellationToken.None)).Should().BeNull();
        (await _cleanup.ListPendingFileDeletionsAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
        MockFiles.Received(1).TryDelete(OwnerUserId, "photos/trip.jpg");
        await MockRemote.Received(1).GetAsync(record.Detail.Trip.Id, Arg.Any<CancellationToken>());
        MockNotifier.Received(1).NotifyStateChanged();
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
        var record = await CreateConfirmedAsync(withPhotograph: true);
        var view = ToRemote(record);
        var result = failure switch
        {
            "metadata" => ApiResultModel<TripDetailDto>.Succeeded(view with { Trip = view.Trip with { Title = "Old revision" } }),
            "missing-photo" => ApiResultModel<TripDetailDto>.Succeeded(view with { Photographs = [] }),
            "missing-bytes" => ApiResultModel<TripDetailDto>.Succeeded(view with { Photographs = [view.Photographs[0] with { Url = null }] }),
            "401" => ApiResultModel<TripDetailDto>.Failed(ApiOutcomeEnum.SessionExpired),
            _ => ApiResultModel<TripDetailDto>.Failed(ApiOutcomeEnum.Unavailable)
        };
        MockRemote.GetAsync(record.Detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(result);
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await TripStore.GetAsync(OwnerUserId, record.Detail.Trip.Id, CancellationToken.None)).Should().NotBeNull();
        MockFiles.DidNotReceive().TryDelete(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldNotCleanAnActiveEditor()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        using var edit = await _edits.AcquireAsync(record.Detail.Trip.Id, CancellationToken.None);
        using var service = CreateService();
        // Act
        await service.RunAsync(CancellationToken.None);
        // Assert
        (await TripStore.GetAsync(OwnerUserId, record.Detail.Trip.Id, CancellationToken.None)).Should().NotBeNull();
        await MockRemote.DidNotReceive().GetAsync(record.Detail.Trip.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecheckEligibilityInTheDeletionTransaction()
    {
        // Arrange
        var record = await CreateConfirmedAsync();
        var candidate = (await _cleanup.ListCandidatesAsync(OwnerUserId, CancellationToken.None)).Single();
        await TripStore.SaveAsync(OwnerLease, record, record.Revision, CancellationToken.None);
        // Act
        var deleted = await _cleanup.TryDeleteAsync(candidate, DateTimeOffset.UtcNow, CancellationToken.None);
        // Assert
        deleted.Should().BeFalse();
        (await TripStore.GetAsync(OwnerUserId, record.Detail.Trip.Id, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldRecoverQueuedFilesOfflineAfterRestartAndRetryFailedDeletion()
    {
        // Arrange
        var record = await CreateConfirmedAsync(withPhotograph: true);
        var candidate = (await _cleanup.ListCandidatesAsync(OwnerUserId, CancellationToken.None)).Single();
        await _cleanup.TryDeleteAsync(candidate, DateTimeOffset.UtcNow, CancellationToken.None);
        MockConnectivity.IsOnline.Returns(false);
        MockFiles.TryDelete(OwnerUserId, "photos/trip.jpg").Returns(false, true);
        // Act
        using (var first = CreateService())
        {
            await first.RunAsync(CancellationToken.None);
        }

        (await _cleanup.ListPendingFileDeletionsAsync(OwnerUserId, CancellationToken.None)).Should().ContainSingle();
        using var restarted = CreateService();
        await restarted.RunAsync(CancellationToken.None);
        // Assert
        (await _cleanup.ListPendingFileDeletionsAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
        await MockRemote.DidNotReceive().GetAsync(record.Detail.Trip.Id, Arg.Any<CancellationToken>());
    }
}
