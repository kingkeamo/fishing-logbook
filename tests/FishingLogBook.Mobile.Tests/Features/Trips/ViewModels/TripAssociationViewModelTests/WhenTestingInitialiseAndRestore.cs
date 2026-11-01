using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Models;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripAssociationViewModelTests;

public sealed class WhenTestingInitialiseAndRestore : BaseTripAssociationViewModelTest
{
    [Fact]
    public async Task ItShouldJoinTheResolvedTripForANewCatch()
    {
        // Arrange
        Resolves(Association(), TripId);
        // Act
        await Sut.InitializeNewAsync(ViewerId, TripId, CancellationToken.None);
        // Assert
        Sut.ShowAssociation.Should().BeTrue();
        Sut.TripId.Should().Be(TripId);
        Sut.TripTitle.Should().Be("Corrib");
        Sut.CaughtByUserId.Should().Be(ViewerId);
        Sut.CanLeave.Should().BeTrue();
        await MockService.Received(1).ResolveAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldBeStandaloneWithoutATripWhenNothingResolves()
    {
        // Arrange
        Resolves(null);
        // Act
        await Sut.InitializeNewAsync(ViewerId, null, CancellationToken.None);
        // Assert
        Sut.ShowAssociation.Should().BeFalse();
        Sut.ShowStandalone.Should().BeFalse();
        Sut.TripId.Should().BeNull();
        Sut.CaughtByUserId.Should().Be(ViewerId);
    }

    [Fact]
    public async Task ItShouldKeepADraftWithoutATripStandaloneEvenWhenATripIsNowActive()
    {
        // Arrange
        Resolves(Association());
        // Act
        await Sut.RestoreAsync(ViewerId, null, ViewerId, locked: false, CancellationToken.None);
        // Assert
        Sut.ShowAssociation.Should().BeFalse();
        Sut.ShowStandalone.Should().BeTrue();
        Sut.CanRejoin.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotOfferToRejoinWhenNoTripIsActiveOrTheRecordIsLocked()
    {
        // Arrange
        Resolves(null);
        // Act
        await Sut.RestoreAsync(ViewerId, null, ViewerId, locked: false, CancellationToken.None);
        var noTrip = (Sut.ShowStandalone, Sut.CanRejoin);
        await Sut.RestoreAsync(ViewerId, null, ViewerId, locked: true, CancellationToken.None);
        // Assert
        noTrip.Should().Be((false, false));
        Sut.ShowStandalone.Should().BeFalse();
        await MockService.Received(1).ResolveAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRestoreTheSavedTripAndAnglerWhenTheAnglerIsStillListed()
    {
        // Arrange
        var friend = new TripAnglerOptionModel(FriendId, "Sam", false);
        Resolves(Association(null, null, Now, new TripAnglerOptionModel(ViewerId, "Me", true), friend), TripId);
        // Act
        await Sut.RestoreAsync(ViewerId, TripId, FriendId, locked: false, CancellationToken.None);
        // Assert
        Sut.TripId.Should().Be(TripId);
        Sut.CaughtByUserId.Should().Be(FriendId);
    }

    [Fact]
    public async Task ItShouldFallBackToTheViewerWhenTheSavedAnglerIsNoLongerOnTheTrip()
    {
        // Arrange
        Resolves(Association(), TripId);
        // Act
        await Sut.RestoreAsync(ViewerId, TripId, FriendId, locked: false, CancellationToken.None);
        // Assert
        Sut.CaughtByUserId.Should().Be(ViewerId);
    }

    [Fact]
    public async Task ItShouldFlagATripThatIsNoLongerOnTheDeviceWhenTheRecordCanStillChange()
    {
        // Arrange
        Resolves(null, TripId);
        // Act
        await Sut.RestoreAsync(ViewerId, TripId, ViewerId, locked: false, CancellationToken.None);
        // Assert
        Sut.IsUnavailable.Should().BeTrue();
        Sut.TripId.Should().Be(TripId);
        Sut.TripName.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldKeepALockedRecordsTripWithoutFlaggingItUnavailable()
    {
        // Arrange
        Resolves(null, TripId);
        // Act
        await Sut.RestoreAsync(ViewerId, TripId, FriendId, locked: true, CancellationToken.None);
        // Assert
        Sut.IsUnavailable.Should().BeFalse();
        Sut.TripId.Should().Be(TripId);
        Sut.CaughtByUserId.Should().Be(ViewerId);
        Sut.CanLeave.Should().BeFalse();
    }
}
