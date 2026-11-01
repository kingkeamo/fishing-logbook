using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;
using FishingLogBook.Shared.Constants;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripAssociationServiceTests;

public sealed class WhenTestingSession : BaseTripServiceTest
{
    private readonly ITripParticipantClient _participants = Substitute.For<ITripParticipantClient>();

    private TripAssociationService Association => new(TripSut, TripStore, _participants, MockSessionCoordinator, MockConnectivity);

    [Fact]
    public async Task ItShouldResolveNoTripWhenNobodyIsSignedIn()
    {
        // Arrange
        MockSessionCoordinator.Capture().Returns((AccountSessionLeaseModel?)null);
        // Act
        var resolved = await Association.ResolveAsync(null, CancellationToken.None);
        // Assert
        resolved.Should().BeNull();
        await MockClient.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatATripAsUnavailableWhenNobodyIsSignedIn()
    {
        // Arrange
        MockSessionCoordinator.Capture().Returns((AccountSessionLeaseModel?)null);
        // Act
        var available = await Association.IsAvailableAsync(Guid.NewGuid(), CancellationToken.None);
        // Assert
        available.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldTreatATripThatIsNotOnTheDeviceOrNoLongerContributableAsUnavailable()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var trip = await TripSut.StartAsync("Corrib", null, CancellationToken.None);
        // Act
        var started = await Association.IsAvailableAsync(trip.Detail.Trip.Id, CancellationToken.None);
        var unknown = await Association.IsAvailableAsync(Guid.NewGuid(), CancellationToken.None);
        await TripStore.RevokeAsync(OwnerLease, trip.Detail.Trip.Id, CancellationToken.None);
        var revoked = await Association.IsAvailableAsync(trip.Detail.Trip.Id, CancellationToken.None);
        // Assert
        started.Should().BeTrue();
        unknown.Should().BeFalse();
        revoked.Should().BeFalse();
        trip.Detail.Role.Should().Be(TripParticipantConstants.Owner);
    }
}
