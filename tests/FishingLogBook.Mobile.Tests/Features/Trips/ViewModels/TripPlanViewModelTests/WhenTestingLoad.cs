using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Constants;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripPlanViewModelTests;

public sealed class WhenTestingLoad : BaseTripPlanViewModelTest
{
    [Fact]
    public async Task ItShouldReportAMissingTrip()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns((LocalTripRecordModel?)null);

        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Assert
        Sut.NotFound.Should().BeTrue();
        Sut.ShowContent.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldTreatACancelledPlanAsReadOnly()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(Plan(TripConstants.Cancelled));

        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Assert
        Sut.IsCancelled.Should().BeTrue();
        Sut.CanEdit.Should().BeFalse();
        Sut.CanSave.Should().BeFalse();
        Sut.CanCancelTrip.Should().BeFalse();
        Sut.CanInvite.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldTreatSomeoneElsesPlanAsReadOnly()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Plan(owner: Guid.NewGuid(), role: TripParticipantConstants.Participant));

        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Assert
        Sut.IsReadOnly.Should().BeTrue();
        Sut.CanSave.Should().BeFalse();
        Sut.CanCancelTrip.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotOfferInvitesUntilThePlanHasSynchronised()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(Plan(revision: 2, syncedRevision: 1));

        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Assert
        Sut.CanInvite.Should().BeFalse();
        Sut.ShowInviteHint.Should().BeTrue();
        Sut.CanCancelTrip.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotOfferInvitesWhileOffline()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(Plan());

        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Assert
        Sut.CanInvite.Should().BeFalse();
        Sut.ShowInviteHint.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldStartANewPlanTodayAtTheTopOfTheHour()
    {
        // Arrange
        // Act
        await Sut.LoadAsync(null, CancellationToken.None);

        // Assert
        Sut.IsNew.Should().BeTrue();
        Sut.CanEdit.Should().BeTrue();
        Sut.PlannedDate.Should().Be(Now.Date);
        Sut.PlannedTime.Should().Be(TimeSpan.FromHours(12));
        Sut.LocationOptions.Should().ContainSingle(option => option.Name == "Lough Corrib");
        Sut.HeadingText.Should().Be("Plan a trip");
        await MockTrips.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLoadAnExistingPlanIntoTheForm()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(Plan());

        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Assert
        Sut.EditTitle.Should().Be("Corrib");
        Sut.EditPlace.Should().Be("Shore");
        Sut.PlannedDate.Should().Be(Now.AddDays(1).Date);
        Sut.PlannedTime.Should().Be(Now.AddDays(1).TimeOfDay);
        Sut.CanInvite.Should().BeTrue();
        Sut.HeadingText.Should().Be("Planned trip");
    }
}
