using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Enums;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripDetailViewModelTests;

public sealed class WhenTestingActive : BaseTripDetailViewModelTest
{
    [Fact]
    public void ItShouldHaveNoModelUntilATripIsLoaded()
    {
        // Arrange
        // Act
        var active = Sut.Active;
        // Assert
        active.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldDescribeTheTripWithTheStatusFactsAndPermissionsTheViewShows()
    {
        // Arrange
        await LoadAsync(Trip(title: "Corrib", place: "Lough Corrib"));
        // Act
        var active = Sut.Active!;
        // Assert
        active.Trip.Detail.Trip.Id.Should().Be(TripId);
        active.DateHeading.Should().Be("31 Oct 2026");
        active.TitleText.Should().Be("Corrib");
        active.PlaceText.Should().Be("Lough Corrib");
        active.StartedText.Should().Be("09:00");
        active.StatusText.Should().Be(Text["TripActiveLabel"]);
        active.StatusIcon.Should().Be("icon_play.png");
        active.CanManageTrip.Should().BeTrue();
        active.CanRecordCatch.Should().BeTrue();
        active.CanAddCatches.Should().BeTrue();
        active.CanAddPhotographs.Should().BeTrue();
        active.ShowBackToLogbook.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowAFinishedTripAsFinishedWithoutManagementOrRecordingActions()
    {
        // Arrange
        await LoadAsync(Trip(TripConstants.Completed, endedOn: Now.AddHours(-1)));
        // Act
        var active = Sut.Active!;
        // Assert
        active.IsCompleted.Should().BeTrue();
        active.StatusText.Should().Be(Text["TripStatusFinished"]);
        active.StatusIcon.Should().Be("icon_check.png");
        active.CanManageTrip.Should().BeFalse();
        active.CanRecordCatch.Should().BeFalse();
        active.CanAddCatches.Should().BeTrue();
        active.ShowBackToLogbook.Should().BeTrue();
        active.Timeline.Select(row => row.Kind).Should().Equal(TripTimelineKindEnum.Started, TripTimelineKindEnum.Finished);
        active.Timeline[^1].IsLast.Should().BeTrue();
    }
}
