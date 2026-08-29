using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Trips.Components.TripTimeline;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripTimelineTests;

public class WhenTestingRender : BaseTripTimelineTest
{
    [Fact]
    public async Task ItShouldSayNothingHasHappenedYet()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, Array.Empty<TripTimelineItemModel>()));

        // Assert
        cut.Find("#trip-timeline-empty").TextContent.Should()
            .Contain("Nothing has happened on this trip yet.");
        cut.FindAll("#trip-timeline").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldStillRenderTheEntriesWhenTheLocalTimeCannotBeRead()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("no interop"));
        var logging = QuietLogging();
        await using var context = CreateContext(time, logging);

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, new[] { Item(TripTimelineKindEnum.Started, StartedOn) }));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-timeline-started-{StartedOn.ToUnixTimeMilliseconds()}")
                .TextContent.Should().Contain("Fishing started"));
        await logging.Received(1).LogErrorAsync(
            "reading a trip timeline time",
            Arg.Any<InvalidOperationException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotLabelEveryEntryWithItsKind()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var items = new[]
        {
            Item(TripTimelineKindEnum.Note, StartedOn.AddMinutes(15), text: "The wind dropped.", noteId: NoteId),
            Item(TripTimelineKindEnum.Catch, StartedOn.AddMinutes(30), "Pike", catchId: CatchId)
        };

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, items));

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("The wind dropped."));
        cut.Markup.Should().NotContain("· Note");
        cut.Markup.Should().NotContain("· Catch");
        cut.Markup.Should().NotContain("Trip photograph added");
    }

    [Fact]
    public async Task ItShouldRenderACatchWithItsSpeciesAndMeasurements()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                new[]
                {
                    Item(
                        TripTimelineKindEnum.Catch,
                        StartedOn.AddMinutes(30),
                        "Brown Trout",
                        catchId: CatchId,
                        weight: 1.02m,
                        length: 48m)
                })
            .Add(component => component.CatchBaseHref, "/offline/catches"));

        // Assert
        var entry = cut.Find($"#trip-timeline-catch-{CatchId:D}-catch");
        entry.TextContent.Should().Contain("Brown Trout");
        cut.Find($"#trip-timeline-catch-{CatchId:D}-measurements").TextContent.Should().Contain("48 cm");
        cut.Find($"#trip-timeline-catch-{CatchId:D}-link").GetAttribute("href")
            .Should().Be($"/offline/catches/{CatchId:D}/edit");
    }

    [Fact]
    public async Task ItShouldRenderACatchWithNoMeasurementsWithoutAnEmptyLine()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                new[] { Item(TripTimelineKindEnum.Catch, StartedOn.AddMinutes(30), catchId: CatchId) }));

        // Assert
        cut.Find($"#trip-timeline-catch-{CatchId:D}-catch").TextContent.Should().Contain("Catch recorded");
        cut.FindAll($"#trip-timeline-catch-{CatchId:D}-measurements").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRenderARemotePhotographWithoutReadingLocalMedia()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var tripPhotographStore = StoreWithPhotographBytes(1, 2, 3);
        await using var context = CreateContext(tripPhotographStore: tripPhotographStore);

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                new[]
                {
                    Item(
                        TripTimelineKindEnum.Photograph,
                        StartedOn.AddMinutes(20),
                        photographId: PhotographId,
                        photographUrl: "https://storage.test/one.jpg?signed=1")
                })
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.TripId, TripId));

        // Assert
        cut.Find($"#trip-timeline-photograph-{PhotographId:D}-media").GetAttribute("src")
            .Should().Be("https://storage.test/one.jpg?signed=1");
        await tripPhotographStore.DidNotReceive().GetBytesAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRenderAStoredTripPhotographFromTheDevice()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var tripPhotographStore = StoreWithPhotographBytes(1, 2, 3, 4);
        await using var context = CreateContext(tripPhotographStore: tripPhotographStore);

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                new[]
                {
                    Item(
                        TripTimelineKindEnum.Photograph,
                        StartedOn.AddMinutes(20),
                        photographId: PhotographId)
                })
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-timeline-photograph-{PhotographId:D}-media").GetAttribute("src")
                .Should().StartWith("data:image/jpeg;base64,"));
        await tripPhotographStore.Received(1).GetBytesAsync(
            OwnerUserId,
            TripId,
            PhotographId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRenderAStoredCatchThumbnailFromTheDevice()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchStore = CatchStoreWithPhotographBytes(9, 9, 9);
        await using var context = CreateContext(catchStore: catchStore);

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                new[]
                {
                    Item(
                        TripTimelineKindEnum.Catch,
                        StartedOn.AddMinutes(30),
                        "Pike",
                        catchId: CatchId,
                        photographId: PhotographId)
                })
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-timeline-catch-{CatchId:D}-media").GetAttribute("src")
                .Should().StartWith("data:image/jpeg;base64,"));
        await catchStore.Received(1).GetPhotographBytesAsync(
            OwnerUserId,
            CatchId,
            PhotographId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReadLocalMediaForAHistoricalTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var tripPhotographStore = StoreWithPhotographBytes(1, 2, 3);
        var catchStore = CatchStoreWithPhotographBytes(4, 5, 6);
        await using var context = CreateContext(
            tripPhotographStore: tripPhotographStore,
            catchStore: catchStore);

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                new[]
                {
                    Item(TripTimelineKindEnum.Photograph, StartedOn.AddMinutes(20), photographId: PhotographId),
                    Item(
                        TripTimelineKindEnum.Catch,
                        StartedOn.AddMinutes(30),
                        "Pike",
                        catchId: CatchId,
                        photographId: PhotographId)
                })
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.TripId, TripId)
            .Add(component => component.AllowLocalMedia, false));

        // Assert
        cut.FindAll($"#trip-timeline-photograph-{PhotographId:D}-media").Should().BeEmpty();
        await tripPhotographStore.DidNotReceive().GetBytesAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await catchStore.DidNotReceive().GetPhotographBytesAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOfferDeletingANoteWhenTheTripCannotBeEdited()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var deleted = new List<Guid>();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                new[]
                {
                    Item(TripTimelineKindEnum.Note, StartedOn.AddMinutes(15), text: "Windy.", noteId: NoteId)
                })
            .Add(component => component.CanEditNotes, false)
            .Add(component => component.OnDeleteNote, noteId => deleted.Add(noteId)));

        // Assert
        cut.Markup.Should().Contain("Windy.");
        cut.FindAll($"#trip-timeline-note-remove-{NoteId:D}").Should().BeEmpty();
        deleted.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldAskTheParentToDeleteANote()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var deleted = new List<Guid>();
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                new[]
                {
                    Item(TripTimelineKindEnum.Note, StartedOn.AddMinutes(15), text: "Windy.", noteId: NoteId)
                })
            .Add(component => component.CanEditNotes, true)
            .Add(component => component.OnDeleteNote, noteId => deleted.Add(noteId)));

        // Act
        cut.Find($"#trip-timeline-note-remove-{NoteId:D}").Click();

        // Assert
        deleted.Should().Equal(NoteId);
        cut.Find($"#trip-timeline-note-remove-{NoteId:D}").GetAttribute("aria-label")
            .Should().Be("Remove note");
    }

    [Fact]
    public async Task ItShouldNotOfferEditingANoteWhenTheTripCannotBeEdited()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var edited = new List<TripTimelineItemModel>();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                new[]
                {
                    Item(TripTimelineKindEnum.Note, StartedOn.AddMinutes(15), text: "Windy.", noteId: NoteId)
                })
            .Add(component => component.CanEditNotes, false)
            .Add(component => component.OnEditNote, item => edited.Add(item)));

        // Assert
        cut.Markup.Should().Contain("Windy.");
        cut.FindAll($"#trip-timeline-note-edit-{NoteId:D}").Should().BeEmpty();
        edited.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldAskTheParentToEditANote()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var edited = new List<TripTimelineItemModel>();
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                new[]
                {
                    Item(TripTimelineKindEnum.Note, StartedOn.AddMinutes(15), text: "Windy.", noteId: NoteId)
                })
            .Add(component => component.CanEditNotes, true)
            .Add(component => component.OnEditNote, item => edited.Add(item)));

        // Act
        cut.Find($"#trip-timeline-note-edit-{NoteId:D}").Click();

        // Assert
        edited.Should().ContainSingle();
        edited[0].NoteId.Should().Be(NoteId);
        edited[0].Text.Should().Be("Windy.");
        cut.Find($"#trip-timeline-note-edit-{NoteId:D}").GetAttribute("aria-label")
            .Should().Be("Edit note");
    }

    [Fact]
    public async Task ItShouldShowOnlyTheTimeWhenEveryEntryIsOnTheSameDay()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(TestTimeService.WithOffset(TimeSpan.FromHours(1)));
        var items = new[]
        {
            Item(TripTimelineKindEnum.Started, StartedOn),
            Item(TripTimelineKindEnum.Note, StartedOn.AddMinutes(15), text: "Windy.", noteId: NoteId)
        };

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, items));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-timeline-started-{StartedOn.ToUnixTimeMilliseconds()}")
                .TextContent.Should().Contain("07:00"));
        cut.Markup.Should().NotContain("27 Aug");
    }

    [Fact]
    public async Task ItShouldShowTheDateOnEveryEntryWhenTheTripCrossesMidnight()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var lateNight = DateTimeOffset.Parse("2026-08-27T22:30:00Z");
        var afterMidnight = DateTimeOffset.Parse("2026-08-28T01:15:00Z");
        var items = new[]
        {
            Item(TripTimelineKindEnum.Started, lateNight),
            Item(TripTimelineKindEnum.Note, afterMidnight, text: "Still going.", noteId: NoteId)
        };

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, items));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-timeline-started-{lateNight.ToUnixTimeMilliseconds()}")
                .TextContent.Should().Contain("27 Aug"));
        cut.Find($"#trip-timeline-note-{NoteId:D}").TextContent.Should().Contain("28 Aug");
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, new[] { Item(TripTimelineKindEnum.Started, StartedOn) }));

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Pêche commencée"));
    }
}
