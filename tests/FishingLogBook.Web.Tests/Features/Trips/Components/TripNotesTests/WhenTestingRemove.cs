using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripNoteStoreTests;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TripNotesComponent = FishingLogBook.Web.Features.Trips.Components.TripNotes.TripNotes;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripNotesTests;

public class WhenTestingRemove : BaseTripNotesTest
{
    private static readonly Guid FirstNoteId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid SecondNoteId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public async Task ItShouldWarnAndKeepASynchronisedNoteWhenTheServerRefuses()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = new MemoryTripNoteStore();
        await store.SaveAsync(
            Note(FirstNoteId, syncStatus: SyncStatus.Synchronised),
            CancellationToken.None);
        var client = Substitute.For<ITripClient>();
        client.DeleteNoteAsync(TripId, FirstNoteId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Offline."));
        await using var context = CreateContext(store, tripClient: client);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));
        cut.WaitForAssertion(() => cut.Find($"#trip-note-remove-{FirstNoteId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#trip-note-remove-{FirstNoteId:D}").ClickAsync();

        // Assert
        cut.Find("#trip-note-remove-failed").TextContent.Should().Contain("could not be removed");
        store.Count.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldRemoveALocalNoteWithoutContactingTheServer()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await store.SaveAsync(Note(FirstNoteId), CancellationToken.None);
        var client = Substitute.For<ITripClient>();
        await using var context = CreateContext(store, tripClient: client);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));
        cut.WaitForAssertion(() => cut.Find($"#trip-note-remove-{FirstNoteId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#trip-note-remove-{FirstNoteId:D}").ClickAsync();

        // Assert
        store.Count.Should().Be(0);
        await client.DidNotReceive().DeleteNoteAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        cut.FindAll("#trip-notes-list").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldDeleteASynchronisedNoteOnTheServerToo()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await store.SaveAsync(
            Note(FirstNoteId, syncStatus: SyncStatus.Synchronised),
            CancellationToken.None);
        var client = Substitute.For<ITripClient>();
        await using var context = CreateContext(store, tripClient: client);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));
        cut.WaitForAssertion(() => cut.Find($"#trip-note-remove-{FirstNoteId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#trip-note-remove-{FirstNoteId:D}").ClickAsync();

        // Assert
        await client.Received(1).DeleteNoteAsync(
            TripId,
            FirstNoteId,
            Arg.Any<CancellationToken>());
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldNotDeleteAHistoricalNoteWhenTheConfirmationIsCancelled()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        var client = Substitute.For<ITripClient>();
        await using var context = CreateContext(
            store,
            tripClient: client,
            modalService: ConfirmingModalService(confirm: false));
        var cut = context.Render<TripNotesComponent>(parameters => parameters
            .Add(component => component.Trip, CompletedTrip())
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.NoteStorage, TripStorageEnum.Server));

        // Act
        await cut.InvokeAsync(() => cut.Instance.RemoveNoteAsync(FirstNoteId));

        // Assert
        await client.DidNotReceive().DeleteNoteAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldDeleteAHistoricalNoteOnTheServerAfterConfirmation()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        var client = Substitute.For<ITripClient>();
        var changed = 0;
        await using var context = CreateContext(store, tripClient: client);
        var cut = context.Render<TripNotesComponent>(parameters => parameters
            .Add(component => component.Trip, CompletedTrip())
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.NoteStorage, TripStorageEnum.Server)
            .Add(component => component.Changed, () => changed++));

        // Act
        await cut.InvokeAsync(() => cut.Instance.RemoveNoteAsync(FirstNoteId));

        // Assert
        await client.Received(1).DeleteNoteAsync(
            TripId,
            FirstNoteId,
            Arg.Any<CancellationToken>());
        store.DeleteCalls.Should().Be(0);
        changed.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldLeaveTheOtherNotesUntouched()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        var kept = Note(SecondNoteId, "kept", StartedOn.AddHours(2));
        await store.SaveAsync(Note(FirstNoteId, "removed", StartedOn.AddHours(1)), CancellationToken.None);
        await store.SaveAsync(kept, CancellationToken.None);
        await using var context = CreateContext(store);
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));
        cut.WaitForAssertion(() => cut.Find($"#trip-note-remove-{FirstNoteId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#trip-note-remove-{FirstNoteId:D}").ClickAsync();

        // Assert
        store.Count.Should().Be(1);
        var survivor = store.All().Single();
        survivor.Id.Should().Be(SecondNoteId);
        survivor.Text.Should().Be("kept");
        survivor.RecordedOn.Should().Be(kept.RecordedOn);
    }
}
