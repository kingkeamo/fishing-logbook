using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;
using ActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.ActiveTrip.ActiveTrip;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.ActiveTripTests;

public class WhenTestingDiaryComposition : BaseActiveTripTest
{
    [Fact]
    public async Task ItShouldTellTheTripStoryOnceRatherThanBesideStandaloneSections()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = await StoreWithActiveTripAsync();
        var catchStore = QuietCatchStore(CatchFor(TripId));
        await using var context = CreateContext(store, catchStore: catchStore);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-timeline").Should().NotBeNull());
        cut.FindAll("#trip-photographs").Should().BeEmpty();
        cut.FindAll("#trip-notes-list").Should().BeEmpty();
        cut.FindAll("#trip-photographs-empty").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldOfferTheActiveTripActionsWithoutStackingFullWidthButtons()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = await StoreWithActiveTripAsync();
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-actions").Should().NotBeNull());
        cut.Find("#active-trip-record-catch").Should().NotBeNull();
        cut.Find("#active-trip-add-catch").Should().NotBeNull();
        cut.Find("#active-trip-add-photo").Should().NotBeNull();
        cut.Find("#active-trip-finish").Should().NotBeNull();
        cut.Find("#active-trip-update").GetAttribute("href").Should().Be($"/trips/{TripId:D}/edit");
    }

    [Fact]
    public async Task ItShouldOfferAddNoteAsACompactActionOnAnActiveTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = await StoreWithActiveTripAsync();
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-note-start").Should().NotBeNull());
        var trigger = cut.Find("#trip-note-start");
        trigger.ClassName.Should().Contain("mud-fab");
        trigger.GetAttribute("aria-label").Should().Be("Add note");
    }

    [Fact]
    public async Task ItShouldRevealThePhotographPickerOnlyWhenAddPhotoIsChosen()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = await StoreWithActiveTripAsync();
        await using var context = CreateContext(store);
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#active-trip-add-photo").Should().NotBeNull());
        cut.FindAll("#trip-photo-input").Should().BeEmpty();

        // Act
        await cut.Find("#active-trip-add-photo").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-photographs").Should().NotBeNull());
        cut.FindAll("#trip-photo-carousel").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRevealTheCatchSelectorOnlyWhenAddCatchIsChosen()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = await StoreWithActiveTripAsync();
        var catchStore = QuietCatchStore(CatchFor(null));
        await using var context = CreateContext(store, catchStore: catchStore);
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#active-trip-add-catch").Should().NotBeNull());
        cut.FindAll("#catch-selector").Should().BeEmpty();

        // Act
        await cut.Find("#active-trip-add-catch").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-selector").Should().NotBeNull());
        cut.FindAll("#trip-catches-record").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotExposeTheActiveActionsOnACompletedTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(StoredActiveTrip() with
            {
                Status = TripConstants.Completed,
                EndedOn = StartedOn.AddHours(3)
            });
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#active-trip-status").TextContent.Should().Contain("Finished"));
        cut.FindAll("#active-trip-actions").Should().BeEmpty();
        cut.FindAll("#active-trip-record-catch").Should().BeEmpty();
        cut.FindAll("#active-trip-add-catch").Should().BeEmpty();
        cut.FindAll("#active-trip-add-photo").Should().BeEmpty();
        cut.FindAll("#active-trip-finish").Should().BeEmpty();
        cut.Find("#active-trip-logbook").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldStillOfferAddNoteOnACompletedTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(StoredCompletedTrip());
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-note-start").Should().NotBeNull());
        var trigger = cut.Find("#trip-note-start");
        trigger.ClassName.Should().Contain("mud-fab");
        trigger.GetAttribute("aria-label").Should().Be("Add note");
        cut.FindAll("#active-trip-actions").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldStillOfferToRemoveANoteOnACompletedTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var noteId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(StoredCompletedTrip() with
            {
                Notes = [
                    new TripNoteModel(
                        noteId,
                        TripId,
                        OwnerUserId,
                        "a good day, three brownies",
                        StartedOn.AddHours(2))
                ]
            });
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-timeline-note-remove-{noteId:D}").Should().NotBeNull());
    }
}
