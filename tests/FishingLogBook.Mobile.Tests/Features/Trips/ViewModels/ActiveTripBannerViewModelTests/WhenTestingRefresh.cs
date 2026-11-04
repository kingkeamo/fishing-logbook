using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.ActiveTripBannerViewModelTests;

public sealed class WhenTestingRefresh : BaseActiveTripBannerViewModelTest
{
    [Fact]
    public async Task ItShouldShowNoBannerAndOfferToStartWhenThereIsNoActiveTrip()
    {
        // Arrange
        ServeTrips(Summary(Guid.NewGuid(), TripConstants.Completed));
        // Act
        await Sut.RefreshAsync(force: false, CancellationToken.None);
        // Assert
        Sut.HasActiveTrip.Should().BeFalse();
        Sut.CanStart.Should().BeTrue();
        Sut.ShowAction.Should().BeTrue();
        Sut.ActionText.Should().Be(Text["CatchStartFishingTrip"]);
    }

    [Fact]
    public async Task ItShouldShowTheBannerForTheViewersOwnActiveTripOnly()
    {
        // Arrange
        ServeTrips(Summary(Guid.NewGuid(), role: TripParticipantConstants.Participant), Summary(ActiveId));
        // Act
        await Sut.RefreshAsync(force: false, CancellationToken.None);
        // Assert
        Sut.ActiveTripId.Should().Be(ActiveId);
        Sut.HasActiveTrip.Should().BeTrue();
        Sut.CanStart.Should().BeFalse();
        Sut.ShowAction.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotOfferAnythingWhenTheTripsCannotBeRead()
    {
        // Arrange
        MockTrips.LoadAsync(Arg.Any<CancellationToken>()).Returns<FishingLogBook.Mobile.Features.Trips.Models.TripHistoryModel>(_ => throw new UnauthorizedAccessException());
        // Act
        await Sut.RefreshAsync(force: false, CancellationToken.None);
        // Assert
        Sut.HasActiveTrip.Should().BeFalse();
        Sut.ShowAction.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotReloadWithinTheRefreshIntervalUnlessForced()
    {
        // Arrange
        ServeTrips(Summary(ActiveId));
        await Sut.RefreshAsync(force: false, CancellationToken.None);
        // Act
        await Sut.RefreshAsync(force: false, CancellationToken.None);
        await Sut.RefreshAsync(force: true, CancellationToken.None);
        Clock.Advance(TimeSpan.FromSeconds(21));
        await Sut.RefreshAsync(force: false, CancellationToken.None);
        // Assert
        await MockTrips.Received(3).LoadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefreshAgainWhenTheAccountSessionIsActivated()
    {
        // Arrange
        MockTrips.LoadAsync(Arg.Any<CancellationToken>()).Returns<FishingLogBook.Mobile.Features.Trips.Models.TripHistoryModel>(_ => throw new UnauthorizedAccessException());
        await Sut.RefreshAsync(force: false, CancellationToken.None);
        ServeTrips(Summary(ActiveId));
        // Act
        MockSession.SessionActivated += Raise.Event();
        await Task.Delay(100);
        // Assert
        Sut.ActiveTripId.Should().Be(ActiveId);
        await MockTrips.Received(2).LoadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatASessionEndingMidLoadAsNothingToShowInsteadOfFailing()
    {
        // Arrange
        MockTrips.LoadAsync(Arg.Any<CancellationToken>()).Returns<FishingLogBook.Mobile.Features.Trips.Models.TripHistoryModel>(_ => throw new OperationCanceledException());
        // Act
        var act = () => Sut.RefreshAsync(force: false, CancellationToken.None);
        // Assert
        await act.Should().NotThrowAsync();
        Sut.HasActiveTrip.Should().BeFalse();
    }
}
