using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Components.PlannedTripEditor;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.PlannedTripEditorTests;

public class WhenTestingRender : BasePlannedTripEditorTest
{
    [Fact]
    public async Task ItShouldShowAnEmptyFormWithoutAnInviteOrCancelActionForANewPlan()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId));

        // Assert
        cut.Find("#planned-trip-editor-title").TextContent.Should().Contain("Plan a trip");
        cut.Find("#planned-trip-save").Should().NotBeNull();
        cut.FindAll("#planned-trip-invite").Should().BeEmpty();
        cut.FindAll("#planned-trip-cancel-trip").Should().BeEmpty();
        await TripStore.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLoadTheExistingPlanIntoTheForm()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        TripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(Planned());
        await using var context = CreateContext();

        // Act
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId)
            .Add(editor => editor.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#planned-trip-title").GetAttribute("value").Should().Be("Corrib");
            cut.Find("#planned-trip-when").GetAttribute("value").Should().Be("2026-09-12T06:30");
            cut.Find("#planned-trip-invite").HasAttribute("disabled").Should().BeFalse();
        });
        cut.FindAll("#planned-trip-invite-hint").Should().BeEmpty();
        cut.Find("#planned-trip-cancel-trip").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldDisableInvitingWhileOffline()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        Network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        TripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(Planned());
        await using var context = CreateContext();

        // Act
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId)
            .Add(editor => editor.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#planned-trip-invite").HasAttribute("disabled").Should().BeTrue());
        cut.Find("#planned-trip-invite-hint").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldDisableInvitingUntilThePlanHasSynchronised()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        TripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(Planned(syncStatus: SyncStatus.SavedLocally));
        await using var context = CreateContext();

        // Act
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId)
            .Add(editor => editor.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#planned-trip-invite").HasAttribute("disabled").Should().BeTrue());
    }

    [Fact]
    public async Task ItShouldShowACancelledPlanAsReadOnly()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        TripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(Planned(TripConstants.Cancelled));
        await using var context = CreateContext();

        // Act
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId)
            .Add(editor => editor.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#planned-trip-cancelled").Should().NotBeNull());
        cut.FindAll("#planned-trip-save").Should().BeEmpty();
        cut.FindAll("#planned-trip-cancel-trip").Should().BeEmpty();
        cut.FindAll("#planned-trip-invite").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowAPlanOwnedBySomeoneElseAsReadOnly()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        TripStore.GetAsync(OtherUserId, TripId, Arg.Any<CancellationToken>()).Returns(Planned());
        await using var context = CreateContext();

        // Act
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OtherUserId)
            .Add(editor => editor.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#planned-trip-read-only").Should().NotBeNull());
        cut.FindAll("#planned-trip-save").Should().BeEmpty();
        cut.FindAll("#planned-trip-cancel-trip").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId));

        // Assert
        cut.Find("#planned-trip-editor-title").TextContent.Should().NotContain("Plan a trip");
        cut.Find("#planned-trip-save").TextContent.Should().NotContain("Save");
    }
}
