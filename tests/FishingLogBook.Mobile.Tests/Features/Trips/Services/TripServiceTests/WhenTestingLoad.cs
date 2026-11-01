using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;

public sealed class WhenTestingLoad : BaseTripServiceTest
{
    [Fact]
    public async Task ItShouldKeepAnOfflineStartedTripVisibleAndMarkedPendingWhenTheServerIsUnreachable()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var trip = await TripSut.StartAsync("Offline trip", null, CancellationToken.None);
        // Act
        var history = await TripSut.LoadAsync(CancellationToken.None);
        // Assert
        history.ServerUnavailable.Should().BeTrue();
        history.Items.Should().ContainSingle(item => item.Id == trip.Detail.Trip.Id);
        history.PendingIds.Should().Contain(trip.Detail.Trip.Id);
        await MockClient.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotTreatAFailedServerListAsAnEmptyHistoryOrRevokeCachedTrips()
    {
        // Arrange
        var server = ServerTrip(Guid.NewGuid());
        MockClient.GetAsync(server.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(server));
        await TripSut.GetAsync(server.Trip.Id, CancellationToken.None);
        MockClient.ListAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<TripSummaryDto>>.Failed(ApiOutcomeEnum.Unavailable));
        // Act
        var history = await TripSut.LoadAsync(CancellationToken.None);
        // Assert
        history.ServerUnavailable.Should().BeTrue();
        history.Items.Should().ContainSingle(item => item.Id == server.Trip.Id);
        (await TripStore.GetAsync(OwnerUserId, server.Trip.Id, CancellationToken.None))!.AccessRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRevokeACachedServerTripThatNoLongerAppearsInTheServerList()
    {
        // Arrange
        var server = ServerTrip(Guid.NewGuid());
        MockClient.GetAsync(server.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(server));
        await TripSut.GetAsync(server.Trip.Id, CancellationToken.None);
        MockClient.ListAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<TripSummaryDto>>.Succeeded([]));
        // Act
        var history = await TripSut.LoadAsync(CancellationToken.None);
        // Assert
        history.Items.Should().BeEmpty();
        history.ServerUnavailable.Should().BeFalse();
        (await TripStore.GetAsync(OwnerUserId, server.Trip.Id, CancellationToken.None))!.AccessRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldShowTheServerTripsAndAnUnsyncedLocalTripTogetherWithoutDuplicates()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var local = await TripSut.StartAsync("Local", null, CancellationToken.None);
        MockConnectivity.IsOnline.Returns(true);
        var server = ServerTrip(Guid.NewGuid(), status: "Completed");
        MockClient.ListAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<TripSummaryDto>>.Succeeded([Summary(server)]));
        // Act
        var history = await TripSut.LoadAsync(CancellationToken.None);
        // Assert
        history.Items.Select(item => item.Id).Should().BeEquivalentTo([local.Detail.Trip.Id, server.Trip.Id]);
        history.Items.Select(item => item.Id).Should().OnlyHaveUniqueItems();
        history.Items[0].Id.Should().Be(local.Detail.Trip.Id);
    }
}
