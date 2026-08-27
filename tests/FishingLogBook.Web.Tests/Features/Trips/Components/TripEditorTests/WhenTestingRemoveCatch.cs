using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Components.TripEditor;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripEditorTests;

public class WhenTestingRemoveCatch : BaseTripEditorTest
{
    [Fact]
    public async Task ItShouldAskForConfirmationExplainingTheCatchRemainsInTheLogbook()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var associated = AssociatedCatch("Brown Trout");
        var modalService = Substitute.For<IModalService>();
        modalService.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>()).Returns(true);
        var catchStore = QuietCatchStore();
        await using var context = CreateContext(modalService: modalService, catchStore: catchStore);
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip())
            .Add(component => component.Catches, new[] { associated }));

        // Act
        await cut.Find($"#trip-editor-catch-remove-{associated.Id:D}").ClickAsync();

        // Assert
        await modalService.Received(1).ConfirmAsync(
            Arg.Is<ConfirmModalModel>(model =>
                model.Title == "Remove catch from trip?" &&
                model.Message.Contains("Brown Trout") &&
                model.Message.Contains("remain in your logbook")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOnlyUnlinkTheCatchAndLeaveItInTheStoreWhenConfirmed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var associated = AssociatedCatch("Brown Trout");
        var modalService = Substitute.For<IModalService>();
        modalService.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>()).Returns(true);
        var catchStore = QuietCatchStore();
        var changed = 0;
        await using var context = CreateContext(modalService: modalService, catchStore: catchStore);
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip())
            .Add(component => component.Catches, new[] { associated })
            .Add(component => component.OnContentChanged, () => changed++));

        // Act
        await cut.Find($"#trip-editor-catch-remove-{associated.Id:D}").ClickAsync();

        // Assert
        changed.Should().Be(1);
        await catchStore.Received(1).UpdateTripAsync(
            OwnerUserId,
            associated.Id,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLeaveTheCatchOnTheTripWhenCancelled()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var associated = AssociatedCatch("Brown Trout");
        var modalService = Substitute.For<IModalService>();
        modalService.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>()).Returns(false);
        var catchStore = QuietCatchStore();
        var changed = 0;
        await using var context = CreateContext(modalService: modalService, catchStore: catchStore);
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip())
            .Add(component => component.Catches, new[] { associated })
            .Add(component => component.OnContentChanged, () => changed++));

        // Act
        await cut.Find($"#trip-editor-catch-remove-{associated.Id:D}").ClickAsync();

        // Assert
        changed.Should().Be(0);
        cut.Find($"#trip-editor-catch-{associated.Id:D}").Should().NotBeNull();
        await catchStore.DidNotReceive().UpdateTripAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }
}
