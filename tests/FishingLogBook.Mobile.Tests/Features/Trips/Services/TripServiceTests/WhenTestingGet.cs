using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;

public sealed class WhenTestingGet : BaseTripServiceTest
{
    [Fact]
    public async Task ItShouldReturnNothingForATripThatIsNotOnTheDeviceWhileOffline()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        // Act
        var trip = await TripSut.GetAsync(Guid.NewGuid(), CancellationToken.None);
        // Assert
        trip.Should().BeNull();
        await MockClient.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRevokeAndHideAServerTripWhenTheServerNoLongerAllowsAccess()
    {
        // Arrange
        var server = ServerTrip(Guid.NewGuid());
        MockClient.GetAsync(server.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(server));
        await TripSut.GetAsync(server.Trip.Id, CancellationToken.None);
        MockClient.GetAsync(server.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Failed(ApiOutcomeEnum.Forbidden));
        // Act
        var trip = await TripSut.GetAsync(server.Trip.Id, CancellationToken.None);
        // Assert
        trip.Should().BeNull();
        (await TripStore.GetAsync(OwnerUserId, server.Trip.Id, CancellationToken.None))!.AccessRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldKeepACachedTripWhenTheServerIsTemporarilyUnavailable()
    {
        // Arrange
        var server = ServerTrip(Guid.NewGuid());
        MockClient.GetAsync(server.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(server));
        await TripSut.GetAsync(server.Trip.Id, CancellationToken.None);
        MockClient.GetAsync(server.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Failed(ApiOutcomeEnum.Unavailable));
        // Act
        var trip = await TripSut.GetAsync(server.Trip.Id, CancellationToken.None);
        // Assert
        trip.Should().NotBeNull();
        trip!.CanContribute.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldKeepALocallyStartedTripWhenTheServerHasNotSeenItYet()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var local = await TripSut.StartAsync("Not synced", null, CancellationToken.None);
        MockConnectivity.IsOnline.Returns(true);
        MockClient.GetAsync(local.Detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Failed(ApiOutcomeEnum.NotFound));
        // Act
        var trip = await TripSut.GetAsync(local.Detail.Trip.Id, CancellationToken.None);
        // Assert
        trip.Should().NotBeNull();
        trip!.Detail.Trip.Title.Should().Be("Not synced");
        trip.AccessRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldCacheAnActiveServerTripSoItIsAvailableOfflineAfterARestart()
    {
        // Arrange
        var server = ServerTrip(Guid.NewGuid(), role: TripParticipantConstants.Participant, owner: Guid.NewGuid());
        MockClient.GetAsync(server.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(server));
        await TripSut.GetAsync(server.Trip.Id, CancellationToken.None);
        MockConnectivity.IsOnline.Returns(false);
        // Act
        var offline = await new FishingLogBook.Mobile.Features.Trips.Services.TripService(
            CreateTripStore(), MockClient, MockSessionCoordinator, MockConnectivity, Clock)
            .GetAsync(server.Trip.Id, CancellationToken.None);
        // Assert
        offline.Should().NotBeNull();
        offline!.CanContribute.Should().BeTrue();
        offline.CanManage.Should().BeFalse();
        offline.HasPendingMetadata.Should().BeFalse();
    }
}
