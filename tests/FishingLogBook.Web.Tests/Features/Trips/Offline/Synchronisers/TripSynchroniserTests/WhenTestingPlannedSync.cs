using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Synchronisers.TripSynchroniserTests;

public class WhenTestingPlannedSync : BaseTripSynchroniserTest
{
    private static readonly DateTimeOffset PlannedStart = DateTimeOffset.Parse("2026-09-12T06:30:00Z");

    [Fact]
    public async Task ItShouldKeepOfflineWorkPendingWhenTheRebasedWriteFails()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateTrip(status: TripConstants.Planned, title: "Offline title") with { PlannedStartOn = PlannedStart });
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns<TripDto?>(
            _ => throw new HttpRequestException("conflict", null, System.Net.HttpStatusCode.Conflict),
            _ => throw new HttpRequestException("unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable));
        MockTripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(new TripDetailDto(
            new TripViewDto(TripId, OwnerUserId, TripConstants.Active, StartedOn) { PlannedStartOn = PlannedStart }));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var stored = (await store.GetAsync(OwnerUserId, TripId, CancellationToken.None))!;
        stored.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        stored.Title.Should().Be("Offline title");
        await MockTripClient.Received(2).UpsertAsync(Arg.Is<TripDto>(sent => sent.Id == TripId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRebaseOfflineMetadataOnTheAuthoritativeCompletedLifecycleAfterAConflict()
    {
        // Arrange
        var plan = CreateTrip(status: TripConstants.Planned, startedOn: PlannedStart, title: "Offline title") with { PlannedStartOn = PlannedStart };
        var store = await CreateStoreAsync(plan);
        var actualStart = PlannedStart.AddMinutes(10);
        var end = actualStart.AddHours(3);
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns(
            _ => throw new HttpRequestException("conflict", null, System.Net.HttpStatusCode.Conflict),
            call => call.Arg<TripDto>());
        MockTripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(new TripDetailDto(
            new TripViewDto(TripId, OwnerUserId, TripConstants.Completed, actualStart, end) { PlannedStartOn = PlannedStart }));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var stored = (await store.GetAsync(OwnerUserId, TripId, CancellationToken.None))!;
        stored.Status.Should().Be(TripConstants.Completed);
        stored.StartedOn.Should().Be(actualStart);
        stored.EndedOn.Should().Be(end);
        stored.Title.Should().Be("Offline title");
        stored.SyncStatus.Should().Be(SyncStatus.Synchronised);
        await MockTripClient.Received(1).UpsertAsync(Arg.Is<TripDto>(sent => sent.Status == TripConstants.Completed
            && sent.StartedOn == actualStart && sent.Title == "Offline title"), Arg.Any<CancellationToken>());
    }

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
            .Returns<Task<TripDto?>>(async call =>
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
