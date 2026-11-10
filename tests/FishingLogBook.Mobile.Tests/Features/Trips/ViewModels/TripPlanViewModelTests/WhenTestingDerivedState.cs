using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripPlanViewModelTests;

public sealed class WhenTestingDerivedState : BaseTripPlanViewModelTest
{
    [Fact]
    public void ItShouldDescribeANewPlanBeforeAnythingIsLoaded()
    {
        // Arrange
        // Act
        var heading = Sut.HeadingText;

        // Assert
        Sut.IsNew.Should().BeTrue();
        Sut.TripId.Should().BeNull();
        Sut.IsCancelled.Should().BeFalse();
        Sut.IsReadOnly.Should().BeFalse();
        Sut.CanEdit.Should().BeTrue();
        Sut.CanCancelTrip.Should().BeFalse();
        Sut.CanInvite.Should().BeFalse();
        Sut.ShowInviteHint.Should().BeFalse();
        Sut.HasPendingSync.Should().BeFalse();
        Sut.HasMessage.Should().BeFalse();
        Sut.MessageText.Should().BeEmpty();
        heading.Should().Be(Text["TripPlanTitle"]);
    }

    [Fact]
    public async Task ItShouldDescribeAnExistingEditablePlanAndOfferInvitesOnceSynchronised()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(Plan());

        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Assert
        Sut.IsNew.Should().BeFalse();
        Sut.TripId.Should().Be(TripId);
        Sut.HeadingText.Should().Be(Text["TripPlanEditTitle"]);
        Sut.CanEdit.Should().BeTrue();
        Sut.CanCancelTrip.Should().BeTrue();
        Sut.CanInvite.Should().BeTrue();
        Sut.ShowInviteHint.Should().BeFalse();
        Sut.HasPendingSync.Should().BeFalse();
        await MockTrips.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldHintThatInvitesNeedASynchronisedPlanWhileTheFirstSyncIsPending()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(Plan(revision: 1, syncedRevision: 0));

        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Assert
        Sut.CanInvite.Should().BeFalse();
        Sut.ShowInviteHint.Should().BeTrue();
        Sut.HasPendingSync.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldHintThatInvitesNeedAConnectionWhileOffline()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(Plan());
        MockConnectivity.IsOnline.Returns(false);

        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Assert
        Sut.CanCancelTrip.Should().BeTrue();
        Sut.CanInvite.Should().BeFalse();
        Sut.ShowInviteHint.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotCancelAPlanThatCannotBeCancelled()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(Plan(FishingLogBook.Shared.Constants.TripConstants.Cancelled));
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Act
        var cancelled = await Sut.CancelTripAsync(CancellationToken.None);

        // Assert
        cancelled.Should().BeFalse();
        await MockPlanned.DidNotReceive().CancelPlanAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCancelANewPlan()
    {
        // Arrange
        // Act
        var cancelled = await Sut.CancelTripAsync(CancellationToken.None);

        // Assert
        cancelled.Should().BeFalse();
        await MockPlanned.DidNotReceive().CancelPlanAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRereadAPlanWithNothingPendingOrAfterBeingDisposed()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(Plan());
        await Sut.LoadAsync(TripId, CancellationToken.None);
        MockTrips.ClearReceivedCalls();

        // Act
        await Sut.RereadAfterSyncAsync(CancellationToken.None);
        Sut.Dispose();
        Sut.Dispose();
        await Sut.RereadAfterSyncAsync(CancellationToken.None);

        // Assert
        await MockTrips.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRereadWhenThereIsNoPlan()
    {
        // Arrange
        // Act
        await Sut.RereadAfterSyncAsync(CancellationToken.None);

        // Assert
        await MockTrips.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("  Lough Corrib  ", "Lough Corrib")]
    [InlineData("   ", "")]
    public void ItShouldTrimASelectedLocationAndAcceptABlankOne(string selected, string expected)
    {
        // Arrange
        // Act
        Sut.SelectLocation(selected);

        // Assert
        Sut.EditPlace.Should().Be(expected);
    }

    [Fact]
    public void ItShouldNotRaiseAChangeWhenThePlaceOrTitleIsSetToTheSameValue()
    {
        // Arrange
        Sut.EditPlace = "Shore";
        Sut.EditTitle = "Corrib";
        var raised = new List<string?>();
        Sut.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        // Act
        Sut.EditPlace = "Shore";
        Sut.EditTitle = "Corrib";

        // Assert
        raised.Should().BeEmpty();
    }
}
