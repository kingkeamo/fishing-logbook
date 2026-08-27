using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Synchronisers.TripSynchroniserTests;

public class WhenTestingSynchronisePending : BaseTripSynchroniserTest
{
    [Fact]
    public async Task ItShouldDoNothingWhenTheOwnerIsUnknown()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(Guid.Empty, CancellationToken.None);

        // Assert
        store.PendingCalls.Should().Be(0);
        await MockTripClient.DidNotReceive()
            .UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
        MockActiveTripService.DidNotReceive().Invalidate();
    }

    [Fact]
    public async Task ItShouldNotContactTheServerWhenThereIsNothingPending()
    {
        // Arrange
        var store = await CreateStoreAsync(
            CreateTrip(syncStatus: SyncStatus.Synchronised, syncedAt: StartedOn));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        store.PendingCalls.Should().Be(1);
        await MockNetworkService.DidNotReceive().IsOnlineAsync(Arg.Any<CancellationToken>());
        await MockTripClient.DidNotReceive()
            .UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLeaveWorkPendingWhileOffline()
    {
        // Arrange
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.DidNotReceive()
            .UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
        var stored = await store.GetAsync(OwnerUserId, TripId, CancellationToken.None);
        stored!.SyncStatus.Should().Be(SyncStatus.SavedLocally);
    }

    [Fact]
    public async Task ItShouldNotSynchroniseAnotherAnglersTrip()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateTrip(ownerUserId: OtherUserId));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.DidNotReceive()
            .UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
        var stored = await store.GetAsync(OtherUserId, TripId, CancellationToken.None);
        stored!.SyncStatus.Should().Be(SyncStatus.SavedLocally);
    }

    [Fact]
    public async Task ItShouldMarkATripFailedWhenTheServerRejectsIt()
    {
        // Arrange
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns<TripDto?>(_ => throw new HttpRequestException("rejected"));
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var stored = await store.GetAsync(OwnerUserId, TripId, CancellationToken.None);
        stored!.SyncStatus.Should().Be(SyncStatus.FailedToSynchronise);
        stored.SyncedAt.Should().BeNull();
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Error,
            DiagnosticEventNames.TripSyncFailed,
            Arg.Any<string>(),
            Arg.Is<IReadOnlyDictionary<string, string>>(metadata =>
                metadata[DiagnosticMetadata.ErrorType] == nameof(HttpRequestException)),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task ItShouldSendAConflictedTripOnlyOncePerRunAndSettleOnTheNextRun()
    {
        // Arrange
        var attempts = 0;
        var endedOn = StartedOn.AddHours(2);
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new HttpRequestException(
                        "another active trip exists",
                        null,
                        System.Net.HttpStatusCode.Conflict);
                }

                return call.ArgAt<TripDto>(0) with
                {
                    Status = TripConstants.Completed,
                    EndedOn = endedOn
                };
            });
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        var afterConflict = await store.GetAsync(OwnerUserId, TripId, CancellationToken.None);
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        afterConflict!.SyncStatus.Should().Be(SyncStatus.FailedToSynchronise);
        attempts.Should().Be(2);
        var settled = await store.GetAsync(OwnerUserId, TripId, CancellationToken.None);
        settled!.SyncStatus.Should().Be(SyncStatus.Synchronised);
        settled.Status.Should().Be(TripConstants.Completed);
        settled.EndedOn.Should().Be(endedOn);
    }

    [Fact]
    public async Task ItShouldRetryATripThatPreviouslyFailed()
    {
        // Arrange
        var store = await CreateStoreAsync(
            CreateTrip(syncStatus: SyncStatus.FailedToSynchronise));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(trip => trip.Id == TripId),
            Arg.Any<CancellationToken>());
        var stored = await store.GetAsync(OwnerUserId, TripId, CancellationToken.None);
        stored!.SyncStatus.Should().Be(SyncStatus.Synchronised);
    }

    [Fact]
    public async Task ItShouldNotSendTheSameTripTwiceConcurrently()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);
        var release = new TaskCompletionSource();
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await release.Task;
                return call.ArgAt<TripDto>(0);
            });

        // Act
        var first = sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        var second = sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        release.SetResult();
        await Task.WhenAll(first, second);

        // Assert
        await MockTripClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(trip => trip.Id == TripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepATripPendingWhenItChangedLocallyWhileSynchronising()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await store.SaveAsync(
                    CreateTrip(status: TripConstants.Completed, endedOn: StartedOn.AddHours(3)),
                    CancellationToken.None);
                return call.ArgAt<TripDto>(0);
            });

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var stored = await store.GetAsync(OwnerUserId, TripId, CancellationToken.None);
        stored!.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        stored.Status.Should().Be(TripConstants.Completed);
        stored.EndedOn.Should().Be(StartedOn.AddHours(3));
    }

    [Fact]
    public async Task ItShouldNotResurrectATripDeletedLocallyWhileSynchronising()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);
        store.BeforeSingleRead = async tripId =>
        {
            store.BeforeSingleRead = null;
            await store.CleanupSyncedAsync(OwnerUserId, StartedOn, [], CancellationToken.None);
            await store.SaveAsync(
                CreateTrip(
                    tripId: tripId,
                    status: TripConstants.Completed,
                    syncStatus: SyncStatus.Synchronised,
                    syncedAt: StartedOn),
                CancellationToken.None);
            await store.CleanupSyncedAsync(OwnerUserId, StartedOn, [], CancellationToken.None);
        };

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var all = await store.GetAllAsync(OwnerUserId, CancellationToken.None);
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldApplyTheServerLifecycleWhenAnotherTripStartedLater()
    {
        // Arrange
        var endedOn = StartedOn.AddHours(2);
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<TripDto>(0) with
            {
                Status = TripConstants.Completed,
                EndedOn = endedOn
            });
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var stored = await store.GetAsync(OwnerUserId, TripId, CancellationToken.None);
        stored!.Status.Should().Be(TripConstants.Completed);
        stored.EndedOn.Should().Be(endedOn);
        stored.SyncStatus.Should().Be(SyncStatus.Synchronised);
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.TripActiveReconciled,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefreshTheActiveTripOnceForABatch()
    {
        // Arrange
        var store = await CreateStoreAsync(
            CreateTrip(status: TripConstants.Completed, endedOn: StartedOn.AddHours(1)),
            CreateTrip(tripId: SecondTripId, startedOn: StartedOn.AddHours(2)));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.Received(2).UpsertAsync(
            Arg.Any<TripDto>(),
            Arg.Any<CancellationToken>());
        MockActiveTripService.Received(1).Invalidate();
    }

    [Fact]
    public async Task ItShouldNotSendTheSameTripAgainAfterASuccessfulRun()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(trip => trip.Id == TripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSendTheCompleteTripAndRecordTheSynchronisation()
    {
        // Arrange
        var location = CreateLocation();
        var store = await CreateStoreAsync(
            CreateTrip(
                status: TripConstants.Completed,
                endedOn: StartedOn.AddHours(4),
                title: "Evening session",
                placeName: "Corrib shoreline",
                location: location));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(trip =>
                trip.Id == TripId
                && trip.Status == TripConstants.Completed
                && trip.StartedOn == StartedOn
                && trip.EndedOn == StartedOn.AddHours(4)
                && trip.Title == "Evening session"
                && trip.PlaceName == "Corrib shoreline"
                && trip.Location!.Latitude == location.Latitude
                && trip.Location.Longitude == location.Longitude),
            Arg.Any<CancellationToken>());
        var stored = await store.GetAsync(OwnerUserId, TripId, CancellationToken.None);
        stored!.SyncStatus.Should().Be(SyncStatus.Synchronised);
        stored.SyncedAt.Should().NotBeNull();
        stored.Location!.Latitude.Should().Be(location.Latitude);
        MockActiveTripService.Received(1).Invalidate();
    }
}
