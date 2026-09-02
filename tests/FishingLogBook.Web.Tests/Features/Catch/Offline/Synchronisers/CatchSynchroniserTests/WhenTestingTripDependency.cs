using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Synchronisers.CatchSynchroniserTests;

public class WhenTestingTripDependency : BaseCatchSynchroniserTest
{
    private static readonly Guid TripId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid OtherTripId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid SecondCatchId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    [Fact]
    public async Task ItShouldNotSendACatchWhoseTripHasNotReachedTheServer()
    {
        // Arrange
        GivenTripNotReady(TripId);
        var store = await CreateStoreAsync(CreateCatch() with { TripId = TripId });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockCatchClient.DidNotReceive().UpsertAsync(
            Arg.Any<CatchDto>(),
            Arg.Any<CancellationToken>());
        var stored = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);
        stored!.SyncStatus.Should().NotBe(SyncStatus.Synchronised);
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Information,
            DiagnosticEventNames.CatchSyncWaitingForTrip,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillSendAStandaloneCatchWhenAnotherTripIsNotReady()
    {
        // Arrange
        GivenTripNotReady(TripId);
        var store = await CreateStoreAsync(
            CreateCatch() with { TripId = TripId },
            CreateCatch(catchId: SecondCatchId));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == SecondCatchId && dto.TripId == null),
            Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillSendACatchLinkedToADifferentReadyTrip()
    {
        // Arrange
        GivenTripNotReady(TripId);
        var store = await CreateStoreAsync(
            CreateCatch() with { TripId = TripId },
            CreateCatch(catchId: SecondCatchId) with { TripId = OtherTripId });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == SecondCatchId && dto.TripId == OtherTripId),
            Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRetryACatchByHandWhileItsTripIsPending()
    {
        // Arrange
        GivenTripNotReady(TripId);
        var store = await CreateStoreAsync(
            CreateCatch() with { TripId = TripId, SyncStatus = SyncStatus.FailedToSynchronise });
        var sut = CreateSut(store);

        // Act
        await sut.RetryAsync(CatchId, CancellationToken.None);

        // Assert
        await MockCatchClient.DidNotReceive().UpsertAsync(
            Arg.Any<CatchDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSendTheCatchOnceItsTripIsOnTheServer()
    {
        // Arrange
        GivenTripNotReady(TripId);
        var store = await CreateStoreAsync(CreateCatch() with { TripId = TripId });
        var sut = CreateSut(store);
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Act
        MockTripDependency.IsTripReadyForServerAsync(
                OwnerUserId,
                TripId,
                Arg.Any<CancellationToken>())
            .Returns(true);
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId && dto.TripId == TripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSendTheTripOnTheCatchPayload()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch() with { TripId = TripId });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripDependency.Received(1).IsTripReadyForServerAsync(
            OwnerUserId,
            TripId,
            Arg.Any<CancellationToken>());
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto =>
                dto.Id == CatchId
                && dto.TripId == TripId
                && dto.CaughtByUserId == OwnerUserId
                && dto.CaughtByUserId == OwnerUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotConsultTheTripDependencyForAStandaloneCatch()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripDependency.DidNotReceive().IsTripReadyForServerAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.TripId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAskAboutEachTripOnlyOncePerRun()
    {
        // Arrange
        var store = await CreateStoreAsync(
            CreateCatch() with { TripId = TripId },
            CreateCatch(catchId: SecondCatchId) with { TripId = TripId },
            CreateCatch(catchId: Guid.Parse("12345678-1234-1234-1234-123456789012"))
                with
            { TripId = OtherTripId });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripDependency.Received(1).IsTripReadyForServerAsync(
            OwnerUserId,
            TripId,
            Arg.Any<CancellationToken>());
        await MockTripDependency.Received(1).IsTripReadyForServerAsync(
            OwnerUserId,
            OtherTripId,
            Arg.Any<CancellationToken>());
        await MockCatchClient.Received(3).UpsertAsync(
            Arg.Any<CatchDto>(),
            Arg.Any<CancellationToken>());
    }

    private void GivenTripNotReady(Guid tripId)
    {
        MockTripDependency.IsTripReadyForServerAsync(
                Arg.Any<Guid>(),
                tripId,
                Arg.Any<CancellationToken>())
            .Returns(false);
    }
}
