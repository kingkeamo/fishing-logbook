using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Synchronisers.TripSynchroniserTests;

public class WhenTestingPlannedSync : BaseTripSynchroniserTest
{
    private static readonly DateTimeOffset PlannedStart = DateTimeOffset.Parse("2026-09-12T06:30:00Z");

    [Theory]
    [InlineData(TripConstants.Planned)]
    [InlineData(TripConstants.Cancelled)]
    public async Task ItShouldSendThePlannedStartForAPlannedOrCancelledTrip(string status)
    {
        // Arrange
        var store = await CreateStoreAsync(
            CreateTrip(status: status, startedOn: PlannedStart) with { PlannedStartOn = PlannedStart });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(trip => trip.Id == TripId
                && trip.Status == status
                && trip.StartedOn == PlannedStart
                && trip.PlannedStartOn == PlannedStart
                && trip.EndedOn == null),
            Arg.Any<CancellationToken>());
        var stored = await store.GetAsync(OwnerUserId, TripId, CancellationToken.None);
        stored!.SyncStatus.Should().Be(SyncStatus.Synchronised);
        stored.PlannedStartOn.Should().Be(PlannedStart);
    }

    [Fact]
    public async Task ItShouldSendTheActualStartAndKeepThePlannedStartWhenAPlannedTripIsActivated()
    {
        // Arrange
        var actualStart = PlannedStart.AddMinutes(12);
        var store = await CreateStoreAsync(
            CreateTrip(startedOn: actualStart) with { PlannedStartOn = PlannedStart });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(trip => trip.Status == TripConstants.Active
                && trip.StartedOn == actualStart
                && trip.PlannedStartOn == PlannedStart),
            Arg.Any<CancellationToken>());
        var stored = await store.GetAsync(OwnerUserId, TripId, CancellationToken.None);
        stored!.StartedOn.Should().Be(actualStart);
        stored.PlannedStartOn.Should().Be(PlannedStart);
    }

    [Fact]
    public async Task ItShouldApplyTheServerLifecycleWhenAParticipantAlreadyActivatedThePlannedTrip()
    {
        // Arrange
        var serverStart = PlannedStart.AddMinutes(3);
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<TripDto>(0) with
            {
                Status = TripConstants.Active,
                StartedOn = serverStart,
                PlannedStartOn = PlannedStart
            });
        var store = await CreateStoreAsync(
            CreateTrip(status: TripConstants.Planned, startedOn: PlannedStart) with { PlannedStartOn = PlannedStart });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var stored = await store.GetAsync(OwnerUserId, TripId, CancellationToken.None);
        stored!.Status.Should().Be(TripConstants.Active);
        stored.StartedOn.Should().Be(serverStart);
        stored.PlannedStartOn.Should().Be(PlannedStart);
        stored.SyncStatus.Should().Be(SyncStatus.Synchronised);
    }

    [Fact]
    public async Task ItShouldKeepATripPendingWhenItsPlannedStartChangedLocallyWhileSynchronising()
    {
        // Arrange
        var store = await CreateStoreAsync(
            CreateTrip(status: TripConstants.Planned, startedOn: PlannedStart) with { PlannedStartOn = PlannedStart });
        var editedStart = PlannedStart.AddDays(1);
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await store.SaveAsync(
                    CreateTrip(status: TripConstants.Planned, startedOn: editedStart) with
                    {
                        PlannedStartOn = editedStart
                    },
                    CancellationToken.None);
                return call.ArgAt<TripDto>(0);
            });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var stored = await store.GetAsync(OwnerUserId, TripId, CancellationToken.None);
        stored!.PlannedStartOn.Should().Be(editedStart);
        stored.SyncStatus.Should().Be(SyncStatus.SavedLocally);
    }
}
