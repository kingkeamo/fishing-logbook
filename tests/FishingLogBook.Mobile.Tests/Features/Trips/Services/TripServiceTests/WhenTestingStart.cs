using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Shared.Constants;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;

public sealed class WhenTestingStart : BaseTripServiceTest
{
    [Fact]
    public async Task ItShouldRefuseASecondActiveOwnedTrip()
    {
        // Arrange
        await TripSut.StartAsync("First", null, CancellationToken.None);
        // Act
        var act = () => TripSut.StartAsync("Second", null, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await TripStore.ListAsync(OwnerUserId, CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldRejectStartingWithoutASignedInAccount()
    {
        // Arrange
        MockSessionCoordinator.Capture().Returns((FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel?)null);
        // Act
        var act = () => TripSut.StartAsync("Offline", null, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldPersistTheActiveTripOfflineAndQueueExactlyOneMetadataUpsertAcrossARestart()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        // Act
        var trip = await TripSut.StartAsync("  Corrib weekend  ", " Lough Corrib ", CancellationToken.None);
        // Assert
        var reopened = await CreateTripStore().GetAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None);
        reopened.Should().NotBeNull();
        reopened!.Detail.Trip.Title.Should().Be("Corrib weekend");
        reopened.Detail.Trip.PlaceName.Should().Be("Lough Corrib");
        reopened.Detail.Trip.Status.Should().Be(TripConstants.Active);
        reopened.Detail.Trip.StartedOn.Should().Be(Now);
        reopened.CanManage.Should().BeTrue();
        reopened.HasPendingMetadata.Should().BeTrue();
        var pending = await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None);
        pending.Should().ContainSingle(operation => operation.AggregateId == trip.Detail.Trip.Id
            && operation.AggregateType == OutboxAggregateTypeEnum.Trip
            && operation.OperationType == OutboxOperationTypeEnum.UpsertTripMetadata);
        await MockClient.DidNotReceive().UpsertAsync(Arg.Any<FishingLogBook.Shared.Dtos.TripDto>(), Arg.Any<CancellationToken>());
    }
}
