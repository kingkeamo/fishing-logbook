using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripNoteStoreTests;
using NSubstitute;
using TripNotesComponent = FishingLogBook.Web.Features.Trips.Components.TripNotes.TripNotes;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripNotesTests;

public class WhenTestingServerNotesAndAuthors : BaseTripNotesTest
{
    private static readonly Guid FirstNoteId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid SecondNoteId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid GuestId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherGuestId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static TripNoteModel NoteBy(Guid noteId, Guid author, DateTimeOffset recordedOn)
    {
        return new TripNoteModel(noteId, TripId, author, "water dropped", recordedOn, SyncStatus.Synchronised);
    }

    [Fact]
    public async Task ItShouldShowNothingForAServerTripWithNoNotes()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, CompletedTrip())
                .Add(component => component.ViewerUserId, OwnerUserId)
                .Add(component => component.NoteStorage, TripStorageEnum.Server));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-notes").Should().NotBeNull());
        cut.FindAll("#trip-notes-list").Should().BeEmpty();
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldListTheNotesOfAServerTripFromTheTripSnapshotWithoutReadingTheDeviceStore()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<Web.Features.Trips.Offline.Stores.ITripNoteStore>();
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, CompletedTrip(NoteBy(FirstNoteId, OwnerUserId, StartedOn.AddHours(1))))
                .Add(component => component.ViewerUserId, OwnerUserId)
                .Add(component => component.NoteStorage, TripStorageEnum.Server));

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#trip-note-{FirstNoteId:D}").Should().NotBeNull());
        await store.DidNotReceive().GetForTripAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNameAGuestNoteAuthorByTheirDisplayNameOrAsUnknown()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = new MemoryTripNoteStore();
        var contributors = new[]
        {
            new TripContributorDto(GuestId, "Mary", null),
            new TripContributorDto(OtherGuestId, " ", null)
        };
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, CompletedTrip(
                    NoteBy(FirstNoteId, GuestId, StartedOn.AddHours(1)),
                    NoteBy(SecondNoteId, OtherGuestId, StartedOn.AddHours(2))))
                .Add(component => component.ViewerUserId, OwnerUserId)
                .Add(component => component.NoteStorage, TripStorageEnum.Server)
                .Add(component => component.Contributors, contributors));

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#trip-note-author-{FirstNoteId:D}").TextContent.Should().Contain("Mary"));
        cut.Find($"#trip-note-author-{SecondNoteId:D}").TextContent.Should().Contain("Another angler");
        cut.FindAll($"#trip-note-remove-{FirstNoteId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldOrderNotesRecordedAtTheSameTimeByTheirIdentity()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = new MemoryTripNoteStore();
        var sameTime = StartedOn.AddHours(1);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, CompletedTrip(
                    NoteBy(SecondNoteId, OwnerUserId, sameTime),
                    NoteBy(FirstNoteId, OwnerUserId, sameTime)))
                .Add(component => component.ViewerUserId, OwnerUserId)
                .Add(component => component.NoteStorage, TripStorageEnum.Server));

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("[id^='trip-note-'][id*='-dddddddd'],[id^='trip-note-'][id*='-eeeeeeee']")
            .Should().NotBeEmpty());
        var ids = cut.FindAll(".trip-note").Select(element => element.Id).ToArray();
        ids.Should().Equal($"trip-note-{FirstNoteId:D}", $"trip-note-{SecondNoteId:D}");
    }

    [Fact]
    public async Task ItShouldKeepTheWholeLocalTimeStringWhenTheTimeServiceReturnsAShortValue()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = new MemoryTripNoteStore();
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns("09:30");
        await using var context = CreateContext(store, time: time);

        // Act
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, CompletedTrip(NoteBy(FirstNoteId, OwnerUserId, StartedOn.AddHours(1))))
                .Add(component => component.ViewerUserId, OwnerUserId)
                .Add(component => component.NoteStorage, TripStorageEnum.Server));

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#trip-note-time-{FirstNoteId:D}").TextContent.Should().Contain("09:30"));
        await time.Received(1).ToDateTimeLocalValueAsync(StartedOn.AddHours(1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFallBackToTheTripSnapshotAndLogWhenTheDeviceStoreFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<Web.Features.Trips.Offline.Stores.ITripNoteStore>();
        store.GetForTripAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<TripNoteModel>>>(_ => throw new InvalidOperationException("idb"));
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(store, logging: logging);

        // Act
        var cut = context.Render<TripNotesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip(NoteBy(FirstNoteId, OwnerUserId, StartedOn.AddHours(1))))
                .Add(component => component.ViewerUserId, OwnerUserId));

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#trip-note-{FirstNoteId:D}").Should().NotBeNull());
        await logging.Received(1).LogErrorAsync(
            "loading trip notes",
            Arg.Is<Exception>(exception => exception.Message == "idb"),
            CancellationToken.None);
    }
}
