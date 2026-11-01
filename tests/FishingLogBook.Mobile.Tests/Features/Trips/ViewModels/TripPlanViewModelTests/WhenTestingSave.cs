using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Shared.Constants;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripPlanViewModelTests;

public sealed class WhenTestingSave : BaseTripPlanViewModelTest
{
    public WhenTestingSave()
        : base(TimeZoneInfo.CreateCustomTimeZone("Plus2", TimeSpan.FromHours(2), "Plus2", "Plus2"))
    {
    }

    [Fact]
    public async Task ItShouldNotSaveAReadOnlyPlan()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(Plan(TripConstants.Cancelled));
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeFalse();
        await MockPlanned.DidNotReceive().UpdatePlanAsync(
            Arg.Any<LocalTripRecordModel>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAFailedSaveAndStayOpen()
    {
        // Arrange
        await Sut.LoadAsync(null, CancellationToken.None);
        MockPlanned.PlanAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("store"));

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeFalse();
        Sut.HasMessage.Should().BeTrue();
        Sut.MessageText.Should().Be("The plan could not be saved.");
        Sut.CanSave.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldCreateANewPlanFromTheLocalWallClockAsAUtcInstant()
    {
        // Arrange
        await Sut.LoadAsync(null, CancellationToken.None);
        Sut.EditTitle = "Corrib";
        Sut.EditPlace = "Shore";
        Sut.PlannedDate = new DateTime(2026, 11, 2);
        Sut.PlannedTime = new TimeSpan(9, 30, 0);
        MockPlanned.PlanAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Plan());

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeTrue();
        await MockPlanned.Received(1).PlanAsync(
            "Corrib",
            "Shore",
            Arg.Is<DateTimeOffset>(start => start.UtcDateTime == new DateTime(2026, 11, 2, 7, 30, 0, DateTimeKind.Utc)),
            Arg.Any<CancellationToken>());
        Sut.IsNew.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldUpdateAnExistingPlan()
    {
        // Arrange
        var plan = Plan();
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(plan);
        await Sut.LoadAsync(TripId, CancellationToken.None);
        Sut.EditTitle = "Renamed";
        MockPlanned.UpdatePlanAsync(plan, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(plan);

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeTrue();
        await MockPlanned.Received(1).UpdatePlanAsync(plan, "Renamed", "Shore", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await MockPlanned.DidNotReceive().PlanAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepThePlanWhenCancellingIsNotConfirmed()
    {
        // Arrange
        var plan = Plan();
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(plan);
        MockPresenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(false);
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Act
        var cancelled = await Sut.CancelTripAsync(CancellationToken.None);

        // Assert
        cancelled.Should().BeFalse();
        await MockPlanned.DidNotReceive().CancelPlanAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAFailureToCancelThePlan()
    {
        // Arrange
        var plan = Plan();
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(plan);
        MockPlanned.CancelPlanAsync(plan, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("store"));
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Act
        var cancelled = await Sut.CancelTripAsync(CancellationToken.None);

        // Assert
        cancelled.Should().BeFalse();
        Sut.HasMessage.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldCancelThePlanAfterConfirmation()
    {
        // Arrange
        var plan = Plan();
        var cancelledPlan = Plan(TripConstants.Cancelled);
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(plan);
        MockPlanned.CancelPlanAsync(plan, Arg.Any<CancellationToken>()).Returns(cancelledPlan);
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Act
        var cancelled = await Sut.CancelTripAsync(CancellationToken.None);

        // Assert
        cancelled.Should().BeTrue();
        await MockPlanned.Received(1).CancelPlanAsync(plan, Arg.Any<CancellationToken>());
        Sut.IsCancelled.Should().BeTrue();
        Sut.CanEdit.Should().BeFalse();
    }
}
