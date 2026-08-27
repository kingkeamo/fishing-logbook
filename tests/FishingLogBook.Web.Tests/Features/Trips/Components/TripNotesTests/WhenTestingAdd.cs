using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripNoteStoreTests;
using NSubstitute;
using TripNotesComponent = FishingLogBook.Web.Features.Trips.Components.TripNotes.TripNotes;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripNotesTests;

public class WhenTestingAdd : BaseTripNotesTest
{
    private static readonly Guid FirstNoteId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid SecondNoteId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public async Task ItShouldOfferAddNoteOnAnActiveTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));

        // Assert
        cut.Find("#trip-notes").Should().NotBeNull();
        cut.Find("#trip-note-start").TextContent.Should().Contain("Add note");
        cut.FindAll("#trip-note-editor").Should().BeEmpty();
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldNotAllowSavingAWhitespaceOnlyNote()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));
        await cut.Find("#trip-note-start").ClickAsync();

        // Act
        cut.Find("#trip-note-text").Input("   \t  ");

        // Assert
        cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue();
        await cut.Find("#trip-note-save").ClickAsync();
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldNotAllowSavingANoteOverTheCap()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));
        await cut.Find("#trip-note-start").ClickAsync();

        // Act
        cut.Find("#trip-note-text").Input(new string('a', TripConstants.MaxNoteTextLength + 1));

        // Assert
        cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue();
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldKeepTheTypedTextWhenTheLocalWriteFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = new MemoryTripNoteStore { FailWrite = true };
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(store, logging: logging);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));
        await cut.Find("#trip-note-start").ClickAsync();
        cut.Find("#trip-note-text").Input("fish rising near the reeds");

        // Act
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        cut.Find("#trip-note-add-failed").TextContent.Should().Contain("could not be added");
        cut.Find("#trip-note-text").GetAttribute("value").Should().Be("fish rising near the reeds");
        store.Count.Should().Be(0);
        await logging.Received(1).LogErrorAsync(
            "adding a trip note",
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNeverLogTheNoteText()
    {
        // Arrange
        const string secret = "met Sarah about the lease at the bailiff hut";
        var store = new MemoryTripNoteStore { FailWrite = true };
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(store, logging: logging);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));
        await cut.Find("#trip-note-start").ClickAsync();
        cut.Find("#trip-note-text").Input(secret);

        // Act
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Is<string>(operation => operation.Contains(secret)),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Is<Exception>(exception => exception.Message.Contains(secret)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveTheNoteLocallyAndClearTheEditor()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));
        await cut.Find("#trip-note-start").ClickAsync();
        cut.Find("#trip-note-text").Input("  changed to olive nymph  ");

        // Act
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        store.Count.Should().Be(1);
        var stored = store.All().Single();
        stored.Text.Should().Be("changed to olive nymph");
        stored.TripId.Should().Be(TripId);
        stored.OwnerUserId.Should().Be(OwnerUserId);
        stored.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        stored.RecordedOn.Should().NotBe(default);
        stored.RecordedOn.Should().NotBe(StartedOn);
        cut.FindAll("#trip-note-editor").Should().BeEmpty();
        cut.Find("#trip-note-start").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldShowTheNoteImmediatelyWithItsLocalTime()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));
        await cut.Find("#trip-note-start").ClickAsync();
        cut.Find("#trip-note-text").Input("wind picked up");

        // Act
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        var noteId = store.All().Single().Id;
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#trip-note-{noteId:D}").TextContent.Should().Contain("wind picked up");
            cut.Find($"#trip-note-time-{noteId:D}").TextContent.Should().MatchRegex(@"^\d{2}:\d{2}$");
        });
    }

    [Fact]
    public async Task ItShouldRenderStoredNotesOldestFirst()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await store.SaveAsync(
            Note(SecondNoteId, "wind picked up", StartedOn.AddHours(5)),
            CancellationToken.None);
        await store.SaveAsync(
            Note(FirstNoteId, "fish rising near the reeds", StartedOn.AddHours(1)),
            CancellationToken.None);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));

        // Assert
        var rendered = cut.FindAll("#trip-notes-list .trip-note-text")
            .Select(element => element.TextContent.Trim())
            .ToArray();
        rendered.Should().Equal("fish rising near the reeds", "wind picked up");
    }

    [Fact]
    public async Task ItShouldAddSeveralNotesInTheOrderTheyWereWritten()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));

        // Act
        await cut.Find("#trip-note-start").ClickAsync();
        cut.Find("#trip-note-text").Input("first");
        await cut.Find("#trip-note-save").ClickAsync();
        await cut.Find("#trip-note-start").ClickAsync();
        cut.Find("#trip-note-text").Input("second");
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        store.Count.Should().Be(2);
        var rendered = cut.FindAll("#trip-notes-list .trip-note-text")
            .Select(element => element.TextContent.Trim())
            .ToArray();
        rendered.Should().Equal("first", "second");
    }

    [Fact]
    public async Task ItShouldNotifyTheParentThatTheTripChanged()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        var changed = 0;
        await using var context = CreateContext(store);
        var cut = context.Render<TripNotesComponent>(parameters => parameters
            .Add(component => component.Trip, Trip())
            .Add(component => component.Changed, () => changed++));
        await cut.Find("#trip-note-start").ClickAsync();
        cut.Find("#trip-note-text").Input("stopped for lunch");

        // Act
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        changed.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldStillOfferNotesOnACompletedTrip()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, CompletedTrip()));
        await cut.Find("#trip-note-start").ClickAsync();
        cut.Find("#trip-note-text").Input("a good day, three brownies");

        // Act
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        store.Count.Should().Be(1);
        store.All().Single().Text.Should().Be("a good day, three brownies");
    }

    [Fact]
    public async Task ItShouldReadTheStoredNotesRatherThanTheTripSnapshot()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await store.SaveAsync(
            Note(FirstNoteId, "written after the page loaded", StartedOn.AddHours(1)),
            CancellationToken.None);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-note-{FirstNoteId:D}").TextContent
                .Should().Contain("written after the page loaded"));
        store.ForTripCalls.Should().Be(1);
        store.PendingCalls.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldShowNotesOnTheFinishRecapEvenFromAStaleTripSnapshot()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await store.SaveAsync(
            Note(FirstNoteId, "fish rising near the reeds", StartedOn.AddHours(1)),
            CancellationToken.None);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, CompletedTrip()));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-note-{FirstNoteId:D}").TextContent
                .Should().Contain("fish rising near the reeds"));
    }

    [Fact]
    public async Task ItShouldStayUsableWhenTheStoredNotesCannotBeRead()
    {
        // Arrange
        var store = new ThrowingTripNoteStore();
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(store, logging: logging);

        // Act
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip(Note(FirstNoteId, "from the snapshot"))));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-note-{FirstNoteId:D}").TextContent.Should().Contain("from the snapshot"));
        cut.Find("#trip-note-start").Should().NotBeNull();
        await logging.Received(1).LogErrorAsync(
            "loading trip notes",
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchNoteCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));

        // Assert
        cut.Find("#trip-notes").TextContent.Should().Contain("Notes de la sortie");
        cut.Find("#trip-note-start").TextContent.Should().Contain("Ajouter une note");
    }
}
