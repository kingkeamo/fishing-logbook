using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Trips.Components.PlannedTripEditor;
using FishingLogBook.Web.Features.Trips.Modals.TripParticipants;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.PlannedTripEditorTests;

public class WhenTestingCancelAndInvite : BasePlannedTripEditorTest
{
    private IRenderedComponent<PlannedTripEditor> RenderExisting(
        BunitContext context)
    {
        TripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(Planned());
        var closes = 0;
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId)
            .Add(editor => editor.TripId, TripId)
            .Add(editor => editor.OnClosed, () => closes++));
        cut.WaitForAssertion(() => cut.Find("#planned-trip-cancel-trip").Should().NotBeNull());
        return cut;
    }

    [Fact]
    public async Task ItShouldNotCancelWhenTheCancellationIsNotConfirmed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        Modals.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>()).Returns(false);
        await using var context = CreateContext();
        var cut = RenderExisting(context);

        // Act
        await cut.Find("#planned-trip-cancel-trip").ClickAsync();

        // Assert
        await PlannedTrips.DidNotReceive().CancelPlanAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await Synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAFailureWhenThePlanCannotBeCancelled()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        PlannedTrips.CancelPlanAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        await using var context = CreateContext();
        var cut = RenderExisting(context);

        // Act
        await cut.Find("#planned-trip-cancel-trip").ClickAsync();

        // Assert
        cut.Find("#planned-trip-save-failed").Should().NotBeNull();
        await Synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogAndReportAnUnexpectedCancellationFailure()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var failure = new InvalidOperationException("store");
        PlannedTrips.CancelPlanAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).ThrowsAsync(failure);
        await using var context = CreateContext();
        var cut = RenderExisting(context);

        // Act
        await cut.Find("#planned-trip-cancel-trip").ClickAsync();

        // Assert
        cut.Find("#planned-trip-save-failed").Should().NotBeNull();
        await Logging.Received(1).LogErrorAsync("cancelling a planned trip", failure, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCancelAfterConfirmationSynchroniseAndClose()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        PlannedTrips.CancelPlanAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(Planned(TripConstants.Cancelled));
        var closes = 0;
        TripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(Planned());
        await using var context = CreateContext();
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId)
            .Add(editor => editor.TripId, TripId)
            .Add(editor => editor.OnClosed, () => closes++));
        cut.WaitForAssertion(() => cut.Find("#planned-trip-cancel-trip").Should().NotBeNull());

        // Act
        await cut.Find("#planned-trip-cancel-trip").ClickAsync();

        // Assert
        await Modals.Received(1).ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>());
        await PlannedTrips.Received(1).CancelPlanAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>());
        await Synchroniser.Received(1).SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
        closes.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldOpenTheParticipantsModalForTheTripWhenInviting()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        TripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(Planned());
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId)
            .Add(editor => editor.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#planned-trip-invite").HasAttribute("disabled").Should().BeFalse());

        // Act
        await cut.Find("#planned-trip-invite").ClickAsync();

        // Assert
        await Modals.Received(1).ShowAsync<TripParticipantsModal, TripParticipantsModalModel, TripParticipantsModalResult>(
            Arg.Is<TripParticipantsModalModel>(model => model.TripId == TripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCloseWhenBackIsChosen()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var closes = 0;
        await using var context = CreateContext();
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId)
            .Add(editor => editor.OnClosed, () => closes++));

        // Act
        await cut.Find("#planned-trip-back").ClickAsync();

        // Assert
        closes.Should().Be(1);
    }
}
