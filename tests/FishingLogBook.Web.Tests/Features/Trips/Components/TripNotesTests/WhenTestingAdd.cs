using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Modals.AddTripNote;
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
        var modalService = ConfirmingModalService();
        await using var context = CreateContext(store, modalService: modalService);

        // Act
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));

        // Assert
        cut.Find("#trip-notes").Should().NotBeNull();
        cut.Find("#trip-note-start").TextContent.Should().Contain("Add note");
        cut.FindAll("#trip-note-text").Should().BeEmpty();
        await modalService.DidNotReceive()
            .ShowAsync<AddTripNoteModal, AddTripNoteModalModel, AddTripNoteModalResult>(
                Arg.Any<AddTripNoteModalModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOpenTheAddNoteModalForThisTrip()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        var modalService = ConfirmingModalService();
        await using var context = CreateContext(store, modalService: modalService);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));

        // Act
        await cut.Find("#trip-note-start").ClickAsync();

        // Assert
        await modalService.Received(1)
            .ShowAsync<AddTripNoteModal, AddTripNoteModalModel, AddTripNoteModalResult>(
                Arg.Is<AddTripNoteModalModel>(model =>
                    model.TripId == TripId
                    && model.OwnerUserId == OwnerUserId
                    && model.TripStartedOn == StartedOn),
                Arg.Any<CancellationToken>());
        cut.FindAll("#trip-notes-list").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotChangeTheTripWhenTheModalIsDismissed()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        var changed = 0;
        await using var context = CreateContext(store, modalService: ConfirmingModalService());
        var cut = context.Render<TripNotesComponent>(parameters => parameters
            .Add(component => component.Trip, Trip())
            .Add(component => component.Changed, () => changed++));

        // Act
        await cut.Find("#trip-note-start").ClickAsync();

        // Assert
        changed.Should().Be(0);
        cut.FindAll("#trip-notes-list").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowTheNoteImmediatelyWithItsLocalTime()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        var note = Note(FirstNoteId, "wind picked up", StartedOn.AddHours(2));
        await using var context = CreateContext(store, modalService: ModalServiceAdding(note));
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));

        // Act
        await cut.Find("#trip-note-start").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#trip-note-{FirstNoteId:D}").TextContent.Should().Contain("wind picked up");
            cut.Find($"#trip-note-time-{FirstNoteId:D}").TextContent.Should().Be("09:00");
        });
    }

    [Fact]
    public async Task ItShouldKeepTheNotesInTimeOrderWhenAnEarlierTimeIsChosen()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await store.SaveAsync(
            Note(SecondNoteId, "wind picked up", StartedOn.AddHours(5)),
            CancellationToken.None);
        var backdated = Note(FirstNoteId, "fish rising near the reeds", StartedOn.AddHours(1));
        await using var context = CreateContext(store, modalService: ModalServiceAdding(backdated));
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip()));
        cut.WaitForAssertion(() => cut.Find($"#trip-note-{SecondNoteId:D}").Should().NotBeNull());

        // Act
        await cut.Find("#trip-note-start").ClickAsync();

        // Assert
        var rendered = cut.FindAll("#trip-notes-list .trip-note-text")
            .Select(element => element.TextContent.Trim())
            .ToArray();
        rendered.Should().Equal("fish rising near the reeds", "wind picked up");
    }

    [Fact]
    public async Task ItShouldNotifyTheParentThatTheTripChanged()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        var changed = 0;
        var note = Note(FirstNoteId, "stopped for lunch", StartedOn.AddHours(3));
        await using var context = CreateContext(store, modalService: ModalServiceAdding(note));
        var cut = context.Render<TripNotesComponent>(parameters => parameters
            .Add(component => component.Trip, Trip())
            .Add(component => component.Changed, () => changed++));

        // Act
        await cut.Find("#trip-note-start").ClickAsync();

        // Assert
        changed.Should().Be(1);
        cut.Find($"#trip-note-{FirstNoteId:D}").TextContent.Should().Contain("stopped for lunch");
    }

    [Fact]
    public async Task ItShouldStillOfferNotesOnACompletedTrip()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        var modalService = ConfirmingModalService();
        await using var context = CreateContext(store, modalService: modalService);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, CompletedTrip()));

        // Act
        await cut.Find("#trip-note-start").ClickAsync();

        // Assert
        await modalService.Received(1)
            .ShowAsync<AddTripNoteModal, AddTripNoteModalModel, AddTripNoteModalResult>(
                Arg.Is<AddTripNoteModalModel>(model => model.TripStartedOn == StartedOn),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAskTheModalToWriteAHistoricalNoteToTheServer()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        var modalService = ConfirmingModalService();
        await using var context = CreateContext(store, modalService: modalService);
        var cut = context.Render<TripNotesComponent>(parameters => parameters
            .Add(component => component.Trip, CompletedTrip())
            .Add(component => component.NoteStorage, TripStorageEnum.Server));

        // Act
        await cut.Find("#trip-note-start").ClickAsync();

        // Assert
        await modalService.Received(1)
            .ShowAsync<AddTripNoteModal, AddTripNoteModalModel, AddTripNoteModalResult>(
                Arg.Is<AddTripNoteModalModel>(model =>
                    model.TripId == TripId
                    && model.Storage == TripStorageEnum.Server
                    && model.TripEndedOn != null),
                Arg.Any<CancellationToken>());
        store.ForTripCalls.Should().Be(0);
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
