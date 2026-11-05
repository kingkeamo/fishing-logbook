using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripPhotographServiceTests;

public sealed class WhenTestingRemove : BaseTripPhotographServiceTest
{
    private static readonly Guid PhotoId = Guid.Parse("c0000000-0000-0000-0000-000000000001");

    private static LocalTripRecordModel TripWith(Guid contributedBy)
    {
        return Trip(photographs: [new TripPhotographViewDto(PhotoId, "image/jpeg", Now.AddHours(-1), "https://img/1.jpg") { ContributedByUserId = contributedBy }]);
    }

    [Fact]
    public async Task ItShouldNotRemoveWhenNobodyIsSignedIn()
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);
        // Act
        var removed = await Sut.RemoveAsync(TripWith(ViewerId), PhotoId, CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
        await MockTrips.DidNotReceive().SavePhotographAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripPhotographViewDto>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRemoveAPhotographThatIsNotOnTheTrip()
    {
        // Arrange
        // Act
        var removed = await Sut.RemoveAsync(TripWith(ViewerId), Guid.NewGuid(), CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
        await MockTrips.DidNotReceive().SavePhotographAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripPhotographViewDto>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRemoveAnotherAnglersPhotograph()
    {
        // Arrange
        // Act
        var removed = await Sut.RemoveAsync(TripWith(FriendId), PhotoId, CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
        await MockTrips.DidNotReceive().SavePhotographAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripPhotographViewDto>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportFailureWhenTheRemovalCannotBeSaved()
    {
        // Arrange
        var cause = new UnauthorizedAccessException();
        MockTrips.SavePhotographAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripPhotographViewDto>(), Arg.Any<string?>(), true, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(cause));
        // Act
        var removed = await Sut.RemoveAsync(TripWith(ViewerId), PhotoId, CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
        Logger.Entries.Should().ContainSingle(entry => ReferenceEquals(entry.Exception, cause));
    }

    [Fact]
    public async Task ItShouldRemoveTheViewersOwnPhotographAsADeletion()
    {
        // Arrange
        // Act
        var removed = await Sut.RemoveAsync(TripWith(ViewerId), PhotoId, CancellationToken.None);
        // Assert
        removed.Should().BeTrue();
        await MockTrips.Received(1).SavePhotographAsync(
            Arg.Is<LocalTripRecordModel>(trip => trip.Detail.Trip.Id == TripId),
            Arg.Is<TripPhotographViewDto>(photo => photo.Id == PhotoId && photo.ContributedByUserId == ViewerId),
            null,
            true,
            Arg.Any<CancellationToken>());
    }
}
