using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Trips.Modals.AddTripNote;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripNoteStoreTests;
using NSubstitute;
using TripNotesComponent = FishingLogBook.Web.Features.Trips.Components.TripNotes.TripNotes;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripNotesTests;

public class WhenTestingEdit : BaseTripNotesTest
{
    private static readonly Guid FirstNoteId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid SecondNoteId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public async Task ItShouldOpenTheModalWithTheExistingNote()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        var existing = Note(FirstNoteId, "wind picked up", StartedOn.AddHours(2));
        await store.SaveAsync(existing, CancellationToken.None);
        var modalService = ConfirmingModalService();
        await using var context = CreateContext(store, modalService: modalService);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));
        cut.WaitForAssertion(() => cut.Find($"#trip-note-{FirstNoteId:D}").Should().NotBeNull());

        // Act
        await cut.InvokeAsync(() => cut.Instance.EditNoteAsync(FirstNoteId, existing.Text, existing.RecordedOn));

        // Assert
        await modalService.Received(1)
            .ShowAsync<AddTripNoteModal, AddTripNoteModalModel, AddTripNoteModalResult>(
                Arg.Is<AddTripNoteModalModel>(model =>
                    model.TripId == TripId
                    && model.ExistingNote != null
                    && model.ExistingNote.Id == FirstNoteId
                    && model.ExistingNote.Text == "wind picked up"
                    && model.ExistingNote.RecordedOn == existing.RecordedOn),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotChangeTheTripWhenTheEditIsDismissed()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        var existing = Note(FirstNoteId, "wind picked up", StartedOn.AddHours(2));
        await store.SaveAsync(existing, CancellationToken.None);
        var changed = 0;
        await using var context = CreateContext(store, modalService: ConfirmingModalService());
        var cut = context.Render<TripNotesComponent>(parameters => parameters
            .Add(component => component.Trip, Trip())
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.Changed, () => changed++));
        cut.WaitForAssertion(() => cut.Find($"#trip-note-{FirstNoteId:D}").Should().NotBeNull());

        // Act
        await cut.InvokeAsync(() => cut.Instance.EditNoteAsync(FirstNoteId, existing.Text, existing.RecordedOn));

        // Assert
        changed.Should().Be(0);
        cut.Find($"#trip-note-{FirstNoteId:D}").TextContent.Should().Contain("wind picked up");
    }

    [Fact]
    public async Task ItShouldReplaceTheNoteTextAndRepositionItWhenRecordedOnChanges()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        var early = Note(FirstNoteId, "fish rising near the reeds", StartedOn.AddHours(1));
        var late = Note(SecondNoteId, "wind picked up", StartedOn.AddHours(5));
        await store.SaveAsync(early, CancellationToken.None);
        await store.SaveAsync(late, CancellationToken.None);
        var movedLater = early with { Text = "moved to the afternoon", RecordedOn = StartedOn.AddHours(6) };
        var modalService = ModalServiceEditing(movedLater);
        await using var context = CreateContext(store, modalService: modalService);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));
        cut.WaitForAssertion(() => cut.Find($"#trip-note-{SecondNoteId:D}").Should().NotBeNull());

        // Act
        await cut.InvokeAsync(() => cut.Instance.EditNoteAsync(FirstNoteId, early.Text, early.RecordedOn));
        cut.Render(parameters => parameters.Add(component => component.Trip, Trip())
            .Add(component => component.ViewerUserId, OwnerUserId));

        // Assert
        var rendered = cut.FindAll("#trip-notes-list .trip-note-text")
            .Select(element => element.TextContent.Trim())
            .ToArray();
        rendered.Should().Equal("wind picked up", "moved to the afternoon");
    }

    [Fact]
    public async Task ItShouldNotifyTheParentThatTheTripChanged()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        var existing = Note(FirstNoteId, "wind picked up", StartedOn.AddHours(2));
        await store.SaveAsync(existing, CancellationToken.None);
        var edited = existing with { Text = "wind died down" };
        var changed = 0;
        await using var context = CreateContext(store, modalService: ModalServiceEditing(edited));
        var cut = context.Render<TripNotesComponent>(parameters => parameters
            .Add(component => component.Trip, Trip())
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.Changed, () => changed++));
        cut.WaitForAssertion(() => cut.Find($"#trip-note-{FirstNoteId:D}").Should().NotBeNull());

        // Act
        await cut.InvokeAsync(() => cut.Instance.EditNoteAsync(FirstNoteId, existing.Text, existing.RecordedOn));
        cut.Render(parameters => parameters
            .Add(component => component.Trip, Trip())
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.Changed, () => changed++));

        // Assert
        changed.Should().Be(1);
        cut.Find($"#trip-note-{FirstNoteId:D}").TextContent.Should().Contain("wind died down");
    }
}
